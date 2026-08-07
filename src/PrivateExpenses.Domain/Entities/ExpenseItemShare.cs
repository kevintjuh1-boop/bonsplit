namespace PrivateExpenses.Domain.Entities;

public class ExpenseItemShare
{
    public Guid Id { get; set; }
    public Guid ExpenseItemId { get; set; }
    public ExpenseItem? ExpenseItem { get; set; }

    public Guid PersonId { get; set; }
    public Person? Person { get; set; }

    public long AmountCents { get; set; }
}
