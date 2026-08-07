namespace PrivateExpenses.Application.Dtos;

public sealed record ExpenseListItemDto(
    Guid Id,
    string MerchantName,
    DateOnly ExpenseDate,
    long TotalCents,
    string? CategoryName,
    string? CategoryIconKey,
    IReadOnlyList<PersonSummaryDto> PaidBy,
    bool HasReceiptDocument);
