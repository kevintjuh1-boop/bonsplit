namespace PrivateExpenses.Application.Dtos;

public sealed class ExpenseItemInput
{
    public required string Description { get; init; }
    public decimal Quantity { get; init; } = 1m;
    public long? UnitPriceCents { get; init; }
    public required long TotalCents { get; init; }
    public bool IsDiscount { get; init; }
    public bool IsDeposit { get; init; }
    public string? PromotionLabel { get; init; }

    /// <summary>People this line is split equally between, in a stable order (earliest gets any
    /// leftover cent). Ignored when <see cref="CustomShareCents"/> is supplied.</summary>
    public IReadOnlyList<Guid> ParticipantPersonIdsInOrder { get; init; } = [];

    /// <summary>Explicit per-person amounts (already converted from a custom split or percentages).
    /// When set, this overrides the equal split and must sum exactly to <see cref="TotalCents"/>.</summary>
    public IReadOnlyDictionary<Guid, long>? CustomShareCents { get; init; }

    /// <summary>When set, this whole line is for someone outside the tracked household — it gets no
    /// shares at all and instead becomes an open entry on the Extern page. Mutually exclusive with
    /// <see cref="ParticipantPersonIdsInOrder"/> and <see cref="CustomShareCents"/>.</summary>
    public string? ExternalRecipientName { get; init; }
}

public sealed class ExpensePaymentInput
{
    public required Guid PersonId { get; init; }
    public required long AmountCents { get; init; }
}

public sealed class CreateExpenseRequest
{
    public required string MerchantName { get; init; }
    public required DateOnly ExpenseDate { get; init; }
    public required long TotalCents { get; init; }
    public Guid? CategoryId { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<ExpenseItemInput> Items { get; init; }
    public required IReadOnlyList<ExpensePaymentInput> Payments { get; init; }
    public Guid? ReceiptDocumentId { get; init; }

    /// <summary>Who saved this expense, if known — used to attribute and address the "nieuwe bon"
    /// notification sent to the other people when a receipt-linked expense is created.</summary>
    public Guid? CreatedByPersonId { get; init; }
}

/// <summary>A manual expense (section 42) has no line items — it's a single amount split between
/// the selected people. It reuses the exact same share/payment/balance logic as receipt-based
/// expenses by being turned into a <see cref="CreateExpenseRequest"/> with one implicit item.</summary>
public sealed class ManualExpenseRequest
{
    public required string Description { get; init; }
    public string? MerchantName { get; init; }
    public required DateOnly ExpenseDate { get; init; }
    public required long AmountCents { get; init; }
    public Guid? CategoryId { get; init; }
    public string? Notes { get; init; }
    public required IReadOnlyList<ExpensePaymentInput> Payments { get; init; }
    public required IReadOnlyList<Guid> ParticipantPersonIdsInOrder { get; init; }
    public IReadOnlyDictionary<Guid, long>? CustomShareCents { get; init; }
}
