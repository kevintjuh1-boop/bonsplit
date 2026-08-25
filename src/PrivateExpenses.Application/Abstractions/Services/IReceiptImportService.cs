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

    /// <summary>Attaches an additional page (e.g. a second page a store prints its BTW breakdown on)
    /// to an already-uploaded document, before it's parsed. Returns the new page's id.</summary>
    Task<Guid> AddPageAsync(
        Guid documentId, Stream content, string originalFileName, string mimeType, long fileSize, CancellationToken cancellationToken = default);

    Task<ReceiptFileContent?> OpenPageFileAsync(Guid pageId, CancellationToken cancellationToken = default);

    Task<ReceiptDocumentDto?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<ReceiptParseResult?> GetLastParseResultAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<List<DuplicateMatchDto>> CheckForDuplicatesByExpenseInfoAsync(
        string merchantName, DateOnly date, long totalCents, CancellationToken cancellationToken = default);

    Task<ReceiptFileContent?> OpenFileAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<List<PendingReceiptDto>> GetPendingReviewAsync(CancellationToken cancellationToken = default);

    /// <summary>Permanently removes a scanned-but-never-saved document (and its stored files) — only
    /// allowed while it's still pending review, never once it's linked to a saved expense.</summary>
    Task DeletePendingAsync(Guid documentId, CancellationToken cancellationToken = default);
}
