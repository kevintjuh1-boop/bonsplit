namespace PrivateExpenses.Application.Dtos;

public sealed record PersonBalanceDto(
    Guid PersonId,
    string Name,
    string Initial,
    string ColorKey,
    long TotalPaidCents,
    long TotalOwedCents,
    long SettlementNetCents,
    long NetBalanceCents);

public sealed record SuggestedDebtDto(
    Guid FromPersonId, string FromName, string FromInitial, string FromColorKey,
    Guid ToPersonId, string ToName, string ToInitial, string ToColorKey,
    long AmountCents);
