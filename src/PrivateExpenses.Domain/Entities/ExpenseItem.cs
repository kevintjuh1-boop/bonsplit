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

    /// <summary>Human-readable promotion type when known (e.g. "1+1 gratis", "20% korting"),
    /// distinct from the generic <see cref="IsDiscount"/> flag. Null when not a discount or when
    /// the specific promotion type wasn't identifiable.</summary>
    public string? PromotionLabel { get; set; }

    /// <summary>Set when this whole line is for someone outside the tracked household (e.g. a friend's
    /// share of the receipt) rather than being split between <see cref="Shares"/>. Mutually exclusive
    /// with having any shares — tracked separately on the Extern page instead of the 3-person saldi.</summary>
    public string? ExternalRecipientName { get; set; }
    public bool IsExternalSettled { get; set; }
    public DateTime? ExternalSettledAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<ExpenseItemShare> Shares { get; set; } = new List<ExpenseItemShare>();
}
