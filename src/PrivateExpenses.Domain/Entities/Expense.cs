namespace PrivateExpenses.Domain.Entities;

public class Expense
{
    public Guid Id { get; set; }
    public required string MerchantName { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public long TotalCents { get; set; }
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    public string? Notes { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ExpenseItem> Items { get; set; } = new List<ExpenseItem>();
    public ICollection<ExpensePayment> Payments { get; set; } = new List<ExpensePayment>();
    public ICollection<ReceiptDocument> Documents { get; set; } = new List<ReceiptDocument>();
}
