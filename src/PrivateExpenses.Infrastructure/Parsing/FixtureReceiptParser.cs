using Microsoft.Extensions.Logging;
using PrivateExpenses.Application.Abstractions.Parsing;
using PrivateExpenses.Application.Dtos.Receipts;

namespace PrivateExpenses.Infrastructure.Parsing;

/// <summary>
/// Returns a fixed, clearly-labeled sample result instead of calling a real AI/OCR provider. Useful
/// for trying out the full upload → review → save flow locally without an API key. This must never be
/// the default in a non-development environment — <see cref="DependencyInjection"/> only wires it up
/// when <c>ReceiptParsing:Provider</c> is explicitly set to "Fixture".
/// </summary>
public class FixtureReceiptParser(ILogger<FixtureReceiptParser> logger) : IReceiptParser
{
    public string ProviderName => "fixture";

    public Task<ReceiptParseResult> ParseAsync(ReceiptParseRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("FixtureReceiptParser: returning canned sample data instead of really reading {FileName}", request.OriginalFileName);

        var items = new List<ReceiptParsedItem>
        {
            new() { Description = "Melk halfvol 1L", Quantity = 2, UnitPriceCents = 129, TotalPriceCents = 258, Confidence = 0.95 },
            new() { Description = "Brood volkoren", Quantity = 1, UnitPriceCents = 249, TotalPriceCents = 249, Confidence = 0.94 },
            new() { Description = "Kaas jong belegen 500g", Quantity = 1, UnitPriceCents = 695, TotalPriceCents = 695, Confidence = 0.9 },
            new() { Description = "Cola 1.5L", Quantity = 1, UnitPriceCents = 229, TotalPriceCents = 229, Confidence = 0.92 },
            new() { Description = "Statiegeld", Quantity = 1, TotalPriceCents = 25, IsDeposit = true, Confidence = 0.8 },
            new() { Description = "Bonuskorting", TotalPriceCents = -100, IsDiscount = true, PromotionLabel = "1+1 gratis", Confidence = 0.75 },
        };

        var total = items.Sum(i => i.TotalPriceCents ?? 0);

        var result = new ReceiptParseResult
        {
            Success = true,
            MerchantName = "Jumbo (voorbeeldbon)",
            Date = DateOnly.FromDateTime(DateTime.Today),
            Currency = "EUR",
            TotalCents = total,
            SubtotalCents = total,
            PaymentMethod = "PIN",
            SuggestedCategoryName = "Boodschappen",
            Items = items,
            ConfidenceWarnings = ["Dit is voorbeelddata van de fixture-parser, geen echte bonherkenning."],
        };

        return Task.FromResult(result);
    }
}
