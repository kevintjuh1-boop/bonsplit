using PrivateExpenses.Application.Dtos.Receipts;

namespace PrivateExpenses.Application.Abstractions.Services;

/// <summary>
/// Orchestrates the receipt upload → store → parse → review flow (section 7/11). Blazor components
/// call only this; the concrete storage and parser providers stay behind IReceiptStorage and
/// IReceiptParser in Infrastructure.
/// </summary>
public interface IReceiptImportService
{
    Task<ReceiptUploadResult> UploadAsync(
        Stream content, string originalFileName, string mimeType, long fileSize, CancellationToken cancellationToken = default);

    Task<ReceiptParseResult> ParseAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<ReceiptDocumentDto?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<ReceiptParseResult?> GetLastParseResultAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<List<DuplicateMatchDto>> CheckForDuplicatesByExpenseInfoAsync(
        string merchantName, DateOnly date, long totalCents, CancellationToken cancellationToken = default);

    Task<ReceiptFileContent?> OpenFileAsync(Guid documentId, CancellationToken cancellationToken = default);
}
