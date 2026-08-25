namespace PrivateExpenses.Application.Dtos;

/// <summary>One receipt line that's entirely for someone outside the tracked household, surfaced on
/// the Extern page so it can be requested and checked off — kept out of the 3-person saldi entirely.</summary>
public sealed record ExternalShareDto(
    Guid ExpenseItemId,
    Guid ExpenseId,
    string RecipientName,
    string ItemDescription,
    string MerchantName,
    DateOnly ExpenseDate,
    long AmountCents,
    bool IsSettled,
    DateTime? SettledAt);
