namespace PrivateExpenses.Domain.Entities;

public class Category
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string IconKey { get; set; }
    public int SortOrder { get; set; }

    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}
