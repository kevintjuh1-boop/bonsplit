namespace PrivateExpenses.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; }
    public required string Message { get; set; }
    public Guid? ExpenseId { get; set; }
    public required Guid RecipientPersonId { get; set; }
    public Guid? ActorPersonId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
