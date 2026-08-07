namespace PrivateExpenses.Application.Dtos;

public sealed class ExpenseFilter
{
    public string? SearchText { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? PayerPersonId { get; init; }
    public Guid? InvolvesPersonId { get; init; }
    public long? MinAmountCents { get; init; }
    public long? MaxAmountCents { get; init; }

    public enum SortOption
    {
        DateDescending,
        DateAscending,
        AmountDescending,
        AmountAscending,
    }

    public SortOption Sort { get; init; } = SortOption.DateDescending;
}
