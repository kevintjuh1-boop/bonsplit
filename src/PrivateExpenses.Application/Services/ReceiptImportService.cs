using System.Text.Json;
using PrivateExpenses.Application.Abstractions.Parsing;
using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Application.Abstractions.Services;
using PrivateExpenses.Application.Abstractions.Storage;
using PrivateExpenses.Application.Dtos.Receipts;
using PrivateExpenses.Application.Exceptions;
using PrivateExpenses.Application.Validation;
using PrivateExpenses.Domain.Entities;
using PrivateExpenses.Domain.Enums;

namespace PrivateExpenses.Application.Services;

public class ReceiptImportService(
    IUnitOfWorkFactory unitOfWorkFactory,
    IReceiptStorage receiptStorage,
    IReceiptParser receiptParser) : IReceiptImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<ReceiptUploadResult> UploadAsync(
        Stream content, string originalFileName, string mimeType, long fileSize, CancellationToken cancellationToken = default)
    {
        // The incoming stream (a Blazor IBrowserFile read stream) isn't seekable, but validation needs
        // to peek at the header and storage needs to read the whole thing — buffer once into a seekable
        // copy, capped just above the size limit so an oversized/misreported upload can't run away.
        using var buffered = new MemoryStream();
        await content.CopyToAsync(buffered, cancellationToken);
        buffered.Position = 0;

        var header = new byte[16];
        var headerLength = await ReadHeaderAsync(buffered, header, cancellationToken);
        buffered.Position = 0;

        var validation = ReceiptFileValidator.Validate(originalFileName, mimeType, fileSize, header.AsSpan(0, headerLength));
        if (!validation.IsValid)
        {
            throw new ExpenseValidationException(validation.ErrorMessage!);
        }

        var stored = await receiptStorage.SaveAsync(buffered, originalFileName, mimeType, cancellationToken);

        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);

        var duplicatesByHash = await uow.ReceiptDocuments.GetByHashAsync(stored.Sha256Hash, cancellationToken);
        var duplicateMatches = duplicatesByHash
            .Where(d => d.Expense is { IsDeleted: false })
            .Select(d => new DuplicateMatchDto(d.Expense!.Id, d.Expense.MerchantName, d.Expense.ExpenseDate, d.Expense.TotalCents))
            .DistinctBy(d => d.ExpenseId)
            .ToList();

        var document = new ReceiptDocument
        {
            Id = Guid.NewGuid(),
            OriginalFileName = originalFileName,
            StoredFileName = stored.StoredFileName,
            MimeType = mimeType,
            FileSize = stored.FileSize,
            FileHash = stored.Sha256Hash,
            UploadedAt = DateTime.UtcNow,
            ParsingStatus = ParsingStatus.Uploaded,
            CreatedAt = DateTime.UtcNow,
        };

        await uow.ReceiptDocuments.AddAsync(document, cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);

        return new ReceiptUploadResult(document.Id, duplicateMatches);
    }

    public async Task<ReceiptParseResult> ParseAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var document = await uow.ReceiptDocuments.GetByIdAsync(documentId, cancellationToken)
            ?? throw new ExpenseValidationException("Dit bondocument bestaat niet (meer).");

        document.ParsingStatus = ParsingStatus.Processing;
        uow.ReceiptDocuments.Update(document);
        await uow.SaveChangesAsync(cancellationToken);

        ReceiptParseResult result;
        try
        {
            await using var fileStream = await receiptStorage.OpenAsync(document.StoredFileName, cancellationToken);
            result = await receiptParser.ParseAsync(new ReceiptParseRequest(fileStream, document.MimeType, document.OriginalFileName), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Parsing must never crash the upload flow (section 59/109) — a provider failure just
            // routes the user to manual entry with a clear message.
            result = ReceiptParseResult.Failed("Bon kon niet automatisch worden uitgelezen door een onverwachte fout.");
        }

        document.ParsingProvider = receiptParser.ProviderName;
        document.ParsingStatus = result.Success ? ParsingStatus.NeedsReview : ParsingStatus.Failed;
        document.ParsingError = result.Success ? null : result.ErrorMessage;
        document.RawStructuredResult = JsonSerializer.Serialize(result, JsonOptions);

        uow.ReceiptDocuments.Update(document);
        await uow.SaveChangesAsync(cancellationToken);

        return result;
    }

    public async Task<ReceiptDocumentDto?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var document = await uow.ReceiptDocuments.GetByIdAsync(documentId, cancellationToken);
        return document is null ? null : ToDto(document);
    }

    public async Task<ReceiptParseResult?> GetLastParseResultAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var document = await uow.ReceiptDocuments.GetByIdAsync(documentId, cancellationToken);
        if (document?.RawStructuredResult is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<ReceiptParseResult>(document.RawStructuredResult, JsonOptions);
    }

    public async Task<List<DuplicateMatchDto>> CheckForDuplicatesByExpenseInfoAsync(
        string merchantName, DateOnly date, long totalCents, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var matches = await uow.ReceiptDocuments.FindPossibleDuplicatesAsync(merchantName, date, totalCents, cancellationToken);
        return matches
            .Where(d => d.Expense is { IsDeleted: false })
            .Select(d => new DuplicateMatchDto(d.Expense!.Id, d.Expense.MerchantName, d.Expense.ExpenseDate, d.Expense.TotalCents))
            .DistinctBy(d => d.ExpenseId)
            .ToList();
    }

    public async Task<ReceiptFileContent?> OpenFileAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var document = await uow.ReceiptDocuments.GetByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            return null;
        }

        var stream = await receiptStorage.OpenAsync(document.StoredFileName, cancellationToken);
        return new ReceiptFileContent(stream, document.MimeType, document.OriginalFileName);
    }

    private static async Task<int> ReadHeaderAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }

    private static ReceiptDocumentDto ToDto(ReceiptDocument document) => new(
        document.Id, document.ExpenseId, document.OriginalFileName, document.MimeType, document.FileSize,
        document.UploadedAt, document.ParsingStatus, document.ParsingProvider, document.ParsingError);
}
