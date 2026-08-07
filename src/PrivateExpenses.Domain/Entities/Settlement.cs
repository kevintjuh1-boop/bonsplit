namespace PrivateExpenses.Domain.Entities;

public class Settlement
{
    public Guid Id { get; set; }
    public Guid FromPersonId { get; set; }
    public Person? FromPerson { get; set; }

    public Guid ToPersonId { get; set; }
    public Person? ToPerson { get; set; }

    public long AmountCents { get; set; }
    public DateOnly SettlementDate { get; set; }
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
}
