namespace PrivateExpenses.Application.Dtos;

/// <summary>One product/item share within a person's monthly statement — their own portion of a
/// receipt line, not the line's full price (e.g. a €9 item split 3 ways shows as €3 here).</summary>
public sealed record PersonMonthlyStatementLineDto(
    string Description,
    string MerchantName,
    DateOnly ExpenseDate,
    long ShareCents);

/// <summary>A kassabon-style rollup of everything one person's share came to in a given month, across
/// every real receipt/expense — "what did I actually have, and what did it add up to".</summary>
public sealed record PersonMonthlyStatementDto(
    Guid PersonId,
    string PersonName,
    DateOnly MonthStart,
    IReadOnlyList<PersonMonthlyStatementLineDto> Lines,
    long TotalCents);
