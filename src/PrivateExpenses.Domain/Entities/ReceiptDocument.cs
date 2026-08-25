using PrivateExpenses.Domain.Enums;

namespace PrivateExpenses.Domain.Entities;

public class ReceiptDocument
{
    public Guid Id { get; set; }
    public Guid? ExpenseId { get; set; }
    public Expense? Expense { get; set; }

    public required string OriginalFileName { get; set; }
    public required string StoredFileName { get; set; }
    public required string MimeType { get; set; }
    public long FileSize { get; set; }
    public required string FileHash { get; set; }

    public DateTime UploadedAt { get; set; }

    public ParsingStatus ParsingStatus { get; set; } = ParsingStatus.Uploaded;
    public string? ParsingProvider { get; set; }
    public string? ParsingError { get; set; }

    /// <summary>Serialized ReceiptParseResult JSON, kept for audit/debugging of what the parser returned.</summary>
    public string? RawStructuredResult { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Additional pages of the same physical receipt (e.g. a second page a store prints its
    /// BTW breakdown on), beyond the primary page held on this document's own fields. Sent to the
    /// parser together with the primary page as one logical document, and shown alongside it on the
    /// review page.</summary>
    public ICollection<ReceiptDocumentPage> ExtraPages { get; set; } = new List<ReceiptDocumentPage>();
}
