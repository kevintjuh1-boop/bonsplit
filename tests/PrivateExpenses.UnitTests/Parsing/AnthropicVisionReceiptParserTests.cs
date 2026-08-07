using PrivateExpenses.Infrastructure.Parsing;

namespace PrivateExpenses.UnitTests.Parsing;

public class AnthropicVisionReceiptParserTests
{
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
