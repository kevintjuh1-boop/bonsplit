namespace PrivateExpenses.Application.Dtos;

public sealed record ExpenseItemShareDto(Guid PersonId, string PersonName, string PersonInitial, string PersonColorKey, long AmountCents);

public sealed record ExpenseItemDto(
    Guid Id,
    string Description,
    decimal Quantity,
    long? UnitPriceCents,
    long TotalCents,
    bool IsDiscount,
    bool IsDeposit,
    string? PromotionLabel,
    int SortOrder,
    IReadOnlyList<ExpenseItemShareDto> Shares);

public sealed record ExpensePaymentDto(Guid PersonId, string PersonName, string PersonInitial, string PersonColorKey, long AmountCents);

public sealed record PersonNetDto(
    Guid PersonId, string PersonName, string PersonInitial, string PersonColorKey,
    long PaidCents, long OwedCents, long NetCents);

public sealed record ExpenseDetailDto(
    Guid Id,
    string MerchantName,
    DateOnly ExpenseDate,
    long TotalCents,
    Guid? CategoryId,
    string? CategoryName,
    string? CategoryIconKey,
    string? Notes,
    bool IsDeleted,
    IReadOnlyList<ExpenseItemDto> Items,
    IReadOnlyList<ExpensePaymentDto> Payments,
    IReadOnlyList<PersonNetDto> PersonTotals,
    IReadOnlyList<Guid> ReceiptDocumentIds);
