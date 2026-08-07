namespace PrivateExpenses.Domain.Entities;

public class ExpensePayment
{
    public Guid Id { get; set; }
    public Guid ExpenseId { get; set; }
    public Expense? Expense { get; set; }

    public Guid PersonId { get; set; }
    public Person? Person { get; set; }

    public long AmountCents { get; set; }
}
