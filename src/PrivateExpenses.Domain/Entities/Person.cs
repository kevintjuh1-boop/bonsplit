namespace PrivateExpenses.Domain.Entities;

public class Person
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Initial { get; set; }
    public required string ColorKey { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<ExpenseItemShare> ExpenseItemShares { get; set; } = new List<ExpenseItemShare>();
    public ICollection<ExpensePayment> ExpensePayments { get; set; } = new List<ExpensePayment>();
}
