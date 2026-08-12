using Microsoft.Extensions.Logging.Abstractions;
using PrivateExpenses.Infrastructure.Parsing;

namespace PrivateExpenses.UnitTests.Parsing;

public class AnthropicVisionReceiptParserTests
{
    private static readonly AnthropicVisionReceiptParser Parser =
        new(client: null!, modelId: "test-model", NullLogger<AnthropicVisionReceiptParser>.Instance);

    [Fact]
    public void MapToResult_ItemWithPromotionLabel_PreservesTheLabel()
    {
        const string json = """
            {
              "merchantName": "Lidl",
              "items": [
                { "description": "Kiwi gold los", "totalPriceAmount": "-1.00", "isDiscount": true, "promotionLabel": "1+1 gratis" }
              ]
            }
            """;

        var result = Parser.MapToResult(json);

        Assert.True(result.Success);
        var item = Assert.Single(result.Items);
        Assert.True(item.IsDiscount);
        Assert.Equal("1+1 gratis", item.PromotionLabel);
    }

    [Theory]
    [InlineData("Boodschappen", "Boodschappen")]
    [InlineData("boodschappen", "Boodschappen")]
    [InlineData(" Eten & drinken ", "Eten & drinken")]
    public void MapToResult_SuggestedCategoryMatchingAKnownCategory_IsAcceptedCaseInsensitively(string suggested, string expected)
    {
        var json = $$"""{ "merchantName": "Lidl", "suggestedCategory": "{{suggested}}", "items": [] }""";

        var result = Parser.MapToResult(json);

        Assert.Equal(expected, result.SuggestedCategoryName);
    }

    [Fact]
    public void MapToResult_SuggestedCategoryNotInTheFixedList_IsDropped()
    {
        // Never trust a hallucinated or malformed category name — the caller matches this straight
        // against real Category rows, so an unknown name must come through as null, not as garbage.
        const string json = """{ "merchantName": "Lidl", "suggestedCategory": "Huisdieren", "items": [] }""";

        var result = Parser.MapToResult(json);

        Assert.Null(result.SuggestedCategoryName);
    }

    [Fact]
    public void MapToResult_ItemWithoutPromotionLabel_LeavesItNull()
    {
        const string json = """
            {
              "merchantName": "Lidl",
              "items": [
                { "description": "Melk", "totalPriceAmount": "1.29" }
              ]
            }
            """;

        var result = Parser.MapToResult(json);

        var item = Assert.Single(result.Items);
        Assert.Null(item.PromotionLabel);
    }

    [Theory]
    [InlineData("12.34", 1234)]
    [InlineData("0.01", 1)]
    [InlineData("-1.00", -100)]
    [InlineData("-0.25", -25)]
    [InlineData("100", 10000)]
    [InlineData("0", 0)]
    [InlineData("5.5", 550)]
    public void ParseAmountToCentsOrNull_ValidDecimalStrings_ConvertsExactly(string input, long expectedCents)
    {
        var result = AnthropicVisionReceiptParser.ParseAmountToCentsOrNull(input);

        Assert.Equal(expectedCents, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a number")]
    [InlineData("€12,34")]
    public void ParseAmountToCentsOrNull_MissingOrUnparseableValue_ReturnsNull(string? input)
    {
        var result = AnthropicVisionReceiptParser.ParseAmountToCentsOrNull(input);

        Assert.Null(result);
    }

    [Fact]
    public void ParseAmountToCentsOrNull_NeverInventsAValue_NullStaysNull()
    {
        // The model is instructed to emit null rather than guess when a price isn't printed (section
        // 15). The parser must preserve that "unknown" state rather than defaulting to zero, which
        // would silently turn "not printed" into a false financial fact.
        var result = AnthropicVisionReceiptParser.ParseAmountToCentsOrNull(null);

        Assert.Null(result);
        Assert.NotEqual(0L, result.GetValueOrDefault(long.MinValue));
    }
}
