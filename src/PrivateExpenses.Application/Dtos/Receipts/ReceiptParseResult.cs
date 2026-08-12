namespace PrivateExpenses.Application.Dtos.Receipts;

public sealed class ReceiptParseResult
{
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public string? MerchantName { get; init; }
    public DateOnly? Date { get; init; }
    public TimeOnly? Time { get; init; }
    public string? Currency { get; init; }

    public long? SubtotalCents { get; init; }
    public long? DiscountCents { get; init; }
    public long? DepositCents { get; init; }
    public long? TaxCents { get; init; }
    public long? TotalCents { get; init; }
    public string? PaymentMethod { get; init; }

    /// <summary>Best-matching category name from the app's fixed category list, when the provider
    /// could confidently determine one from the merchant/items. Null when uncertain — matched
    /// case-insensitively against existing categories by the caller, never trusted as an exact key.</summary>
    public string? SuggestedCategoryName { get; init; }

    public List<ReceiptParsedItem> Items { get; init; } = [];
    public List<string> ConfidenceWarnings { get; init; } = [];

    /// <summary>The raw provider response, kept only for the ReceiptDocument audit trail — never
    /// logged with full contents (section 62).</summary>
    public string? RawProviderResponse { get; init; }

    public static ReceiptParseResult Failed(string errorMessage) => new() { Success = false, ErrorMessage = errorMessage };
}

public sealed class ReceiptParsedItem
{
    public required string Description { get; init; }
    public decimal? Quantity { get; init; }
    public long? UnitPriceCents { get; init; }
    public long? TotalPriceCents { get; init; }
    public bool IsDiscount { get; init; }
    public bool IsDeposit { get; init; }

    /// <summary>Human-readable promotion type when the provider could identify one (e.g. "1+1
    /// gratis", "20% korting"). Null when not a discount or the type wasn't identifiable.</summary>
    public string? PromotionLabel { get; init; }

    /// <summary>0.0–1.0 confidence from the provider, when it reports one. Null when the provider
    /// doesn't supply confidence — never invented (section 84).</summary>
    public double? Confidence { get; init; }

    public string? SourceText { get; init; }
}
