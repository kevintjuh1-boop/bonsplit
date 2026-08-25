using Microsoft.Extensions.Logging.Abstractions;
using PrivateExpenses.Infrastructure.Parsing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

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

    [Fact]
    public async Task PrepareImageForUploadAsync_ImageOverAnthropicsPixelLimit_IsDownscaledToFitUnderIt()
    {
        // Regression test: Anthropic hard-rejects any image over 8000px in either dimension — a real
        // failure a user hit with a full-resolution phone photo of a long Sligro receipt. 8500x100
        // reproduces "one dimension way past the limit" cheaply (850k pixels, not a real photo's
        // megapixel count) while still exercising the exact code path that broke.
        using var oversized = new Image<Rgba32>(8500, 100);
        await using var buffered = new MemoryStream();
        await oversized.SaveAsync(buffered, new PngEncoder());

        var (base64Data, mimeType) = await Parser.PrepareImageForUploadAsync(buffered, "image/png", CancellationToken.None);

        Assert.Equal("image/jpeg", mimeType);
        var resizedBytes = Convert.FromBase64String(base64Data);
        using var resized = Image.Load(resizedBytes);
        Assert.True(resized.Width <= 4000);
        Assert.True(resized.Height <= 4000);
        // Aspect ratio preserved: a 8500x100 source is 85:1, so the resized width should still
        // dominate — this would fail if width/height ever got swapped or resized independently.
        Assert.True(resized.Width > resized.Height * 10);
    }

    [Fact]
    public async Task PrepareImageForUploadAsync_ImageAlreadyUnderTheLimit_IsSentUnmodified()
    {
        using var small = new Image<Rgba32>(800, 600);
        await using var buffered = new MemoryStream();
        await small.SaveAsync(buffered, new PngEncoder());
        var originalBytes = buffered.ToArray();

        var (base64Data, mimeType) = await Parser.PrepareImageForUploadAsync(buffered, "image/png", CancellationToken.None);

        Assert.Equal("image/png", mimeType);
        Assert.Equal(originalBytes, Convert.FromBase64String(base64Data));
    }
}
