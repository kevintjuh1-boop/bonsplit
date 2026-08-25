namespace PrivateExpenses.Domain.Entities;

/// <summary>An additional photo/page belonging to the same physical receipt as its parent
/// <see cref="ReceiptDocument"/> — e.g. a second page a store prints its BTW breakdown on. The
/// document's own <see cref="ReceiptDocument.StoredFileName"/> always holds page 1; this only ever
/// holds page 2 and beyond.</summary>
public class ReceiptDocumentPage
{
    public Guid Id { get; set; }
    public Guid ReceiptDocumentId { get; set; }
    public ReceiptDocument? ReceiptDocument { get; set; }

    public int SortOrder { get; set; }
    public required string StoredFileName { get; set; }
    public required string MimeType { get; set; }
    public long FileSize { get; set; }

    public DateTime CreatedAt { get; set; }
}
