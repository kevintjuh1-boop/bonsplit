namespace PrivateExpenses.Application.Dtos;

/// <summary>One receipt line that's entirely for someone outside the tracked household — kept out of
/// the 3-person saldi entirely, and visible only to <see cref="OwedToPersonId"/>, the person who
/// fronted the money and is owed it back.</summary>
public sealed record ExternalShareDto(
    Guid ExpenseItemId,
    Guid ExpenseId,
    string RecipientName,
    string ItemDescription,
    string MerchantName,
    DateOnly ExpenseDate,
    long AmountCents,
    Guid OwedToPersonId,
    string OwedToPersonName);

/// <summary>A payment registered against a specific (external recipient, owed-to person) pair — see
/// <see cref="ExternalShareDto"/>.</summary>
public sealed record ExternalPaymentDto(
    Guid Id,
    string RecipientName,
    Guid OwedToPersonId,
    string OwedToPersonName,
    long AmountCents,
    DateOnly PaymentDate,
    string? Note);
