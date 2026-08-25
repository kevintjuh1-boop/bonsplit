using PrivateExpenses.Domain.Enums;

namespace PrivateExpenses.Application.Dtos.Receipts;

public sealed record ReceiptDocumentDto(
    Guid Id,
    Guid? ExpenseId,
    string OriginalFileName,
    string MimeType,
    long FileSize,
    DateTime UploadedAt,
    ParsingStatus ParsingStatus,
    string? ParsingProvider,
    string? ParsingError);

public sealed record DuplicateMatchDto(Guid ExpenseId, string MerchantName, DateOnly ExpenseDate, long TotalCents);

/// <summary>A receipt that was scanned but never turned into a saved expense — still sitting in
/// "NeedsReview", findable so the person can pick up where they left off instead of losing it.</summary>
public sealed record PendingReceiptDto(Guid DocumentId, string? MerchantName, DateOnly? Date, long? TotalCents, DateTime UploadedAt);

public sealed record ReceiptUploadResult(Guid DocumentId, IReadOnlyList<DuplicateMatchDto> DuplicateMatches);

public sealed record ReceiptFileContent(Stream Content, string MimeType, string OriginalFileName);
