namespace PrivateExpenses.Domain.Entities;

public class ExpenseItem
{
    public Guid Id { get; set; }
    public Guid ExpenseId { get; set; }
    public Expense? Expense { get; set; }

    public required string Description { get; set; }
    public decimal Quantity { get; set; } = 1m;
    public long? UnitPriceCents { get; set; }
    public long TotalCents { get; set; }
    public int SortOrder { get; set; }

    public bool IsDiscount { get; set; }
    public bool IsDeposit { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<ExpenseItemShare> Shares { get; set; } = new List<ExpenseItemShare>();
}
