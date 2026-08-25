namespace PrivateExpenses.Domain.Entities;

/// <summary>A payment received from someone outside the tracked household (e.g. a friend paying back
/// their share of a receipt), registered against a free-text recipient name and the one person who
/// originally fronted the money — never shown to the other two, since it isn't their money.</summary>
public class ExternalPayment
{
    public Guid Id { get; set; }
    public required string RecipientName { get; set; }

    public Guid OwedToPersonId { get; set; }
    public Person? OwedToPerson { get; set; }

    public long AmountCents { get; set; }
    public DateOnly PaymentDate { get; set; }
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }
}
