using System.Text;
using PrivateExpenses.Application.Abstractions.Parsing;
using PrivateExpenses.Application.Dtos;
using PrivateExpenses.Application.Dtos.Receipts;
using PrivateExpenses.Application.Exceptions;
using PrivateExpenses.Application.Services;
using PrivateExpenses.Domain.Enums;
using PrivateExpenses.IntegrationTests.TestSupport;
using PrivateExpenses.Infrastructure.Parsing;

namespace PrivateExpenses.IntegrationTests;

public class ReceiptImportServiceTests : IAsyncLifetime
{
    private SqliteTestDatabase _db = null!;

    public Task InitializeAsync()
    {
        _db = new SqliteTestDatabase();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    // Minimal but structurally valid JPEG: SOI + APP0/JFIF header + EOI. Enough to pass the
    // magic-byte check without needing a real photo on disk.
    private static byte[] BuildFakeJpegBytes(string uniqueSuffix)
    {
        var marker = Encoding.UTF8.GetBytes(uniqueSuffix);
        var bytes = new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
        };
        return [.. bytes, .. marker, 0xFF, 0xD9];
    }

    private ReceiptImportService CreateService(bool fixtureParser = true)
    {
        var parser = fixtureParser
            ? (Application.Abstractions.Parsing.IReceiptParser)new FixtureReceiptParser(Microsoft.Extensions.Logging.Abstractions.NullLogger<FixtureReceiptParser>.Instance)
            : new DevelopmentReceiptParser();

        return new ReceiptImportService(_db.UnitOfWorkFactory, _db.ReceiptStorage, parser);
    }

    /// <summary>Records exactly what ParseAsync built and sent through — the multi-page wiring can't
    /// be observed via FixtureReceiptParser, which ignores its request entirely.</summary>
    private sealed class RecordingReceiptParser : IReceiptParser
    {
        public string ProviderName => "recording";
        public ReceiptParseRequest? LastRequest { get; private set; }
        public int PageCountSeen { get; private set; }

        public Task<ReceiptParseResult> ParseAsync(ReceiptParseRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            PageCountSeen = 1 + (request.ExtraPages?.Count ?? 0);
            return Task.FromResult(new ReceiptParseResult { Success = true, MerchantName = "Sligro" });
        }
    }

    [Fact]
    public async Task UploadAsync_ValidJpeg_CreatesDocumentWithHash()
    {
        var service = CreateService();
        var bytes = BuildFakeJpegBytes("first-upload");
        using var stream = new MemoryStream(bytes);

        var result = await service.UploadAsync(stream, "bon.jpg", "image/jpeg", bytes.Length);

        var document = await service.GetDocumentAsync(result.DocumentId);
        Assert.NotNull(document);
        Assert.Equal(ParsingStatus.Uploaded, document.ParsingStatus);
        Assert.Empty(result.DuplicateMatches);
    }

    [Fact]
    public async Task UploadAsync_SameFileTwice_SecondUploadReportsNoDuplicateUntilLinkedToExpense()
    {
        // Hash-based duplicate detection only flags a match once the earlier document is actually
        // linked to a saved (non-deleted) expense — an upload still "in review" isn't a duplicate yet.
        var service = CreateService();
        var bytes = BuildFakeJpegBytes("repeat-upload");

        using var firstStream = new MemoryStream(bytes);
        var first = await service.UploadAsync(firstStream, "bon.jpg", "image/jpeg", bytes.Length);

        using var secondStream = new MemoryStream(bytes);
        var second = await service.UploadAsync(secondStream, "bon.jpg", "image/jpeg", bytes.Length);

        Assert.NotEqual(first.DocumentId, second.DocumentId);
        Assert.Empty(second.DuplicateMatches);
    }

    [Fact]
    public async Task UploadAsync_EmptyFile_Throws()
    {
        var service = CreateService();
        using var stream = new MemoryStream([]);

        await Assert.ThrowsAsync<ExpenseValidationException>(() =>
            service.UploadAsync(stream, "bon.jpg", "image/jpeg", 0));
    }

    [Fact]
    public async Task UploadAsync_WrongExtension_Throws()
    {
        var service = CreateService();
        var bytes = "not a receipt"u8.ToArray();
        using var stream = new MemoryStream(bytes);

        await Assert.ThrowsAsync<ExpenseValidationException>(() =>
            service.UploadAsync(stream, "bon.exe", "application/octet-stream", bytes.Length));
    }

    [Fact]
    public async Task UploadAsync_ContentDoesNotMatchDeclaredMimeType_Throws()
    {
        var service = CreateService();
        var bytes = "definitely not a jpeg"u8.ToArray();
        using var stream = new MemoryStream(bytes);

        await Assert.ThrowsAsync<ExpenseValidationException>(() =>
            service.UploadAsync(stream, "bon.jpg", "image/jpeg", bytes.Length));
    }

    [Fact]
    public async Task ParseAsync_WithFixtureParser_SucceedsAndSetsNeedsReview()
    {
        var service = CreateService(fixtureParser: true);
        var bytes = BuildFakeJpegBytes("parse-fixture");
        using var stream = new MemoryStream(bytes);
        var upload = await service.UploadAsync(stream, "bon.jpg", "image/jpeg", bytes.Length);

        var result = await service.ParseAsync(upload.DocumentId);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Items);

        var document = await service.GetDocumentAsync(upload.DocumentId);
        Assert.Equal(ParsingStatus.NeedsReview, document!.ParsingStatus);
        Assert.Equal("fixture", document.ParsingProvider);
    }

    [Fact]
    public async Task GetPendingReviewAsync_IncludesAScannedButUnfinishedReceipt_ThenDropsItOnceLinkedToAnExpense()
    {
        var service = CreateService(fixtureParser: true);
        var bytes = BuildFakeJpegBytes("pending-review");
        using var stream = new MemoryStream(bytes);
        var upload = await service.UploadAsync(stream, "bon.jpg", "image/jpeg", bytes.Length);
        await service.ParseAsync(upload.DocumentId);

        var pending = await service.GetPendingReviewAsync();
        var draft = Assert.Single(pending, p => p.DocumentId == upload.DocumentId);
        Assert.Equal("Jumbo (voorbeeldbon)", draft.MerchantName);

        var expenseService = new ExpenseService(_db.UnitOfWorkFactory);
        var people = await _db.GetPeopleAsync();
        var kevin = people.Single(p => p.Name == "Kevin");
        await expenseService.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = draft.MerchantName!,
            ExpenseDate = draft.Date ?? DateOnly.FromDateTime(DateTime.Today),
            TotalCents = draft.TotalCents ?? 0,
            ReceiptDocumentId = upload.DocumentId,
            Items = [new ExpenseItemInput { Description = "Item", TotalCents = draft.TotalCents ?? 0, ParticipantPersonIdsInOrder = [kevin.Id] }],
            Payments = [new ExpensePaymentInput { PersonId = kevin.Id, AmountCents = draft.TotalCents ?? 0 }],
        });

        Assert.DoesNotContain(await service.GetPendingReviewAsync(), p => p.DocumentId == upload.DocumentId);
    }

    [Fact]
    public async Task ParseAsync_WithDevelopmentParser_FailsCleanlyAndSetsFailedStatus()
    {
        var service = CreateService(fixtureParser: false);
        var bytes = BuildFakeJpegBytes("parse-development");
        using var stream = new MemoryStream(bytes);
        var upload = await service.UploadAsync(stream, "bon.jpg", "image/jpeg", bytes.Length);

        var result = await service.ParseAsync(upload.DocumentId);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);

        var document = await service.GetDocumentAsync(upload.DocumentId);
        Assert.Equal(ParsingStatus.Failed, document!.ParsingStatus);
        Assert.NotNull(document.ParsingError);
    }

    [Fact]
    public async Task UploadAsync_SameFileAsAnAlreadyConfirmedExpense_ReportsDuplicate()
    {
        // Acceptance scenario 7: re-uploading the exact same file after it was already saved as an
        // expense must warn, with a link back to the existing expense.
        var receiptService = CreateService(fixtureParser: true);
        var expenseService = new ExpenseService(_db.UnitOfWorkFactory);
        var people = await _db.GetPeopleAsync();
        var kevin = people.Single(p => p.Name == "Kevin");

        var bytes = BuildFakeJpegBytes("confirmed-duplicate");
        using var firstStream = new MemoryStream(bytes);
        var upload = await receiptService.UploadAsync(firstStream, "bon.jpg", "image/jpeg", bytes.Length);
        await receiptService.ParseAsync(upload.DocumentId);

        var expenseId = await expenseService.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = "Jumbo",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            TotalCents = 1000,
            ReceiptDocumentId = upload.DocumentId,
            Items = [new ExpenseItemInput { Description = "Boodschappen", TotalCents = 1000, ParticipantPersonIdsInOrder = [kevin.Id] }],
            Payments = [new ExpensePaymentInput { PersonId = kevin.Id, AmountCents = 1000 }],
        });

        using var secondStream = new MemoryStream(bytes);
        var secondUpload = await receiptService.UploadAsync(secondStream, "bon.jpg", "image/jpeg", bytes.Length);

        var duplicate = Assert.Single(secondUpload.DuplicateMatches);
        Assert.Equal(expenseId, duplicate.ExpenseId);
    }

    [Fact]
    public async Task GetLastParseResultAsync_AfterParse_ReturnsSameDataForReviewScreenReload()
    {
        var service = CreateService(fixtureParser: true);
        var bytes = BuildFakeJpegBytes("reload-review");
        using var stream = new MemoryStream(bytes);
        var upload = await service.UploadAsync(stream, "bon.jpg", "image/jpeg", bytes.Length);
        var original = await service.ParseAsync(upload.DocumentId);

        var reloaded = await service.GetLastParseResultAsync(upload.DocumentId);

        Assert.NotNull(reloaded);
        Assert.Equal(original.MerchantName, reloaded.MerchantName);
        Assert.Equal(original.Items.Count, reloaded.Items.Count);
    }

    [Fact]
    public async Task AddPageAsync_ThenParseAsync_SendsBothPagesToTheParserAsOneRequest()
    {
        // Regression test for the Sligro case: a second page (the BTW breakdown) must actually reach
        // the parser alongside page 1, in the same call, not just sit unused in storage.
        var recorder = new RecordingReceiptParser();
        var service = new ReceiptImportService(_db.UnitOfWorkFactory, _db.ReceiptStorage, recorder);

        var page1Bytes = BuildFakeJpegBytes("page-1");
        using var page1Stream = new MemoryStream(page1Bytes);
        var upload = await service.UploadAsync(page1Stream, "bon-p1.jpg", "image/jpeg", page1Bytes.Length);

        var page2Bytes = BuildFakeJpegBytes("page-2-btw");
        using var page2Stream = new MemoryStream(page2Bytes);
        await service.AddPageAsync(upload.DocumentId, page2Stream, "bon-p2.jpg", "image/jpeg", page2Bytes.Length);

        await service.ParseAsync(upload.DocumentId);

        Assert.Equal(2, recorder.PageCountSeen);
        Assert.NotNull(recorder.LastRequest!.ExtraPages);
        Assert.Single(recorder.LastRequest.ExtraPages!);

        var document = await service.GetDocumentAsync(upload.DocumentId);
        var extraPage = Assert.Single(document!.ExtraPages);
        Assert.Equal("image/jpeg", extraPage.MimeType);
    }

    [Fact]
    public async Task AddPageAsync_TooManyPages_Throws()
    {
        var service = CreateService(fixtureParser: true);
        var page1Bytes = BuildFakeJpegBytes("too-many-p1");
        using var page1Stream = new MemoryStream(page1Bytes);
        var upload = await service.UploadAsync(page1Stream, "bon.jpg", "image/jpeg", page1Bytes.Length);

        for (var i = 0; i < 5; i++)
        {
            var pageBytes = BuildFakeJpegBytes($"too-many-extra-{i}");
            using var pageStream = new MemoryStream(pageBytes);
            await service.AddPageAsync(upload.DocumentId, pageStream, $"bon-{i}.jpg", "image/jpeg", pageBytes.Length);
        }

        var oneMoreBytes = BuildFakeJpegBytes("too-many-one-more");
        using var oneMoreStream = new MemoryStream(oneMoreBytes);
        await Assert.ThrowsAsync<ExpenseValidationException>(() =>
            service.AddPageAsync(upload.DocumentId, oneMoreStream, "bon-extra.jpg", "image/jpeg", oneMoreBytes.Length));
    }

    [Fact]
    public async Task DeletePendingAsync_RemovesTheDocumentAndItsExtraPages()
    {
        var service = CreateService(fixtureParser: true);
        var page1Bytes = BuildFakeJpegBytes("delete-p1");
        using var page1Stream = new MemoryStream(page1Bytes);
        var upload = await service.UploadAsync(page1Stream, "bon.jpg", "image/jpeg", page1Bytes.Length);

        var page2Bytes = BuildFakeJpegBytes("delete-p2");
        using var page2Stream = new MemoryStream(page2Bytes);
        await service.AddPageAsync(upload.DocumentId, page2Stream, "bon-p2.jpg", "image/jpeg", page2Bytes.Length);
        await service.ParseAsync(upload.DocumentId);

        await service.DeletePendingAsync(upload.DocumentId);

        Assert.Null(await service.GetDocumentAsync(upload.DocumentId));
    }

    [Fact]
    public async Task DeletePendingAsync_AlreadyLinkedToASavedExpense_Throws()
    {
        var service = CreateService(fixtureParser: true);
        var bytes = BuildFakeJpegBytes("delete-linked");
        using var stream = new MemoryStream(bytes);
        var upload = await service.UploadAsync(stream, "bon.jpg", "image/jpeg", bytes.Length);
        await service.ParseAsync(upload.DocumentId);

        var expenseService = new ExpenseService(_db.UnitOfWorkFactory);
        var people = await _db.GetPeopleAsync();
        var kevin = people.Single(p => p.Name == "Kevin");
        await expenseService.CreateAsync(new CreateExpenseRequest
        {
            MerchantName = "Jumbo",
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today),
            TotalCents = 1000,
            ReceiptDocumentId = upload.DocumentId,
            Items = [new ExpenseItemInput { Description = "Item", TotalCents = 1000, ParticipantPersonIdsInOrder = [kevin.Id] }],
            Payments = [new ExpensePaymentInput { PersonId = kevin.Id, AmountCents = 1000 }],
        });

        await Assert.ThrowsAsync<ExpenseValidationException>(() => service.DeletePendingAsync(upload.DocumentId));
    }
}
