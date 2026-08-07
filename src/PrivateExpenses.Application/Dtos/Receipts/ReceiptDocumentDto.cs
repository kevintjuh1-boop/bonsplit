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

public sealed record ReceiptUploadResult(Guid DocumentId, IReadOnlyList<DuplicateMatchDto> DuplicateMatches);

public sealed record ReceiptFileContent(Stream Content, string MimeType, string OriginalFileName);
