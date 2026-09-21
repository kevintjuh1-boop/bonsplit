namespace PrivateExpenses.Application.Dtos;

/// <summary>Total spend across all non-deleted expenses in one calendar month — the raw feed for the
/// Statistieken page's monthly trend chart.</summary>
public sealed record MonthlyTotalDto(DateOnly MonthStart, long TotalCents);
