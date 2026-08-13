namespace PrivateExpenses.Domain.Entities;

public class Person
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Initial { get; set; }
    public required string ColorKey { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    /// <summary>Randomly-generated stored file name for an uploaded profile photo, null when the
    /// person has none (falls back to the initial-letter avatar everywhere it's displayed).</summary>
    public string? AvatarStoredFileName { get; set; }
    public string? AvatarMimeType { get; set; }

    public ICollection<ExpenseItemShare> ExpenseItemShares { get; set; } = new List<ExpenseItemShare>();
    public ICollection<ExpensePayment> ExpensePayments { get; set; } = new List<ExpensePayment>();
}
