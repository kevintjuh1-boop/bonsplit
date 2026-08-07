using PrivateExpenses.Domain.Calculations;
using PrivateExpenses.Domain.Exceptions;

namespace PrivateExpenses.UnitTests.Calculations;

public class MoneySplitterTests
{
    private static readonly Guid Kevin = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Wesley = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Jos = Guid.Parse("00000000-0000-0000-0000-000000000003");

    [Fact]
    public void SplitEqually_1000Cents_2People_SplitsEvenly()
    {
        var result = MoneySplitter.SplitEqually(1000, [Kevin, Wesley]);

        Assert.Equal(500, result[Kevin]);
        Assert.Equal(500, result[Wesley]);
        Assert.Equal(1000, result.Values.Sum());
    }

    [Fact]
    public void SplitEqually_1000Cents_3People_FirstInOrderGetsRemainder()
    {
        var result = MoneySplitter.SplitEqually(1000, [Kevin, Wesley, Jos]);

        Assert.Equal(334, result[Kevin]);
        Assert.Equal(333, result[Wesley]);
        Assert.Equal(333, result[Jos]);
        Assert.Equal(1000, result.Values.Sum());
    }

    [Fact]
    public void SplitEqually_1Cent_3People_OnlyOnePersonGetsIt()
    {
        var result = MoneySplitter.SplitEqually(1, [Kevin, Wesley, Jos]);

        Assert.Equal(1, result[Kevin]);
        Assert.Equal(0, result[Wesley]);
        Assert.Equal(0, result[Jos]);
        Assert.Equal(1, result.Values.Sum());
    }

    [Fact]
    public void SplitEqually_2Cents_3People_TwoPeopleGetOneCent()
    {
        var result = MoneySplitter.SplitEqually(2, [Kevin, Wesley, Jos]);

        Assert.Equal(1, result[Kevin]);
        Assert.Equal(1, result[Wesley]);
        Assert.Equal(0, result[Jos]);
        Assert.Equal(2, result.Values.Sum());
    }

    [Fact]
    public void SplitEqually_999Cents_3People_SplitsEvenlyWithNoRemainder()
    {
        var result = MoneySplitter.SplitEqually(999, [Kevin, Wesley, Jos]);

        Assert.Equal(333, result[Kevin]);
        Assert.Equal(333, result[Wesley]);
        Assert.Equal(333, result[Jos]);
        Assert.Equal(999, result.Values.Sum());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(123456)]
    public void SplitEqually_AnyAmount_3People_NeverLosesOrCreatesCents(long totalCents)
    {
        var result = MoneySplitter.SplitEqually(totalCents, [Kevin, Wesley, Jos]);

        Assert.Equal(totalCents, result.Values.Sum());
        Assert.All(result.Values, v => Assert.True(v >= 0));
    }

    [Fact]
    public void SplitEqually_RemainderOrderIsDeterministic_ChangingOrderChangesWhoGetsExtraCent()
    {
        var kevinFirst = MoneySplitter.SplitEqually(1000, [Kevin, Wesley, Jos]);
        var joshFirst = MoneySplitter.SplitEqually(1000, [Jos, Wesley, Kevin]);

        Assert.Equal(334, kevinFirst[Kevin]);
        Assert.Equal(334, joshFirst[Jos]);
    }

    [Fact]
    public void SplitByPercentages_50_35_15_ConvertsExactlyToCents()
    {
        var result = MoneySplitter.SplitByPercentages(2000, [(Kevin, 50m), (Wesley, 35m), (Jos, 15m)]);

        Assert.Equal(1000, result[Kevin]);
        Assert.Equal(700, result[Wesley]);
        Assert.Equal(300, result[Jos]);
        Assert.Equal(2000, result.Values.Sum());
    }

    [Fact]
    public void SplitByPercentages_ThatDoNotSumTo100_Throws()
    {
        Assert.Throws<MoneySplitException>(() =>
            MoneySplitter.SplitByPercentages(1000, [(Kevin, 50m), (Wesley, 30m)]));
    }

    [Fact]
    public void SplitByPercentages_UnevenSplit_StaysCentExact()
    {
        // 10.00 split 1/3, 1/3, 1/3 by percentage should behave like an equal split.
        var result = MoneySplitter.SplitByPercentages(
            1000,
            [(Kevin, 33.33m), (Wesley, 33.33m), (Jos, 33.34m)]);

        Assert.Equal(1000, result.Values.Sum());
    }

    [Fact]
    public void ValidateExactSplit_CustomAmountsThatSumToTotal_DoesNotThrow()
    {
        var amounts = new Dictionary<Guid, long> { [Kevin] = 1000, [Wesley] = 700, [Jos] = 300 };

        var exception = Record.Exception(() => MoneySplitter.ValidateExactSplit(2000, amounts));

        Assert.Null(exception);
    }

    [Fact]
    public void ValidateExactSplit_CustomAmountsThatDoNotSumToTotal_Throws()
    {
        var amounts = new Dictionary<Guid, long> { [Kevin] = 1000, [Wesley] = 700, [Jos] = 250 };

        Assert.Throws<MoneySplitException>(() => MoneySplitter.ValidateExactSplit(2000, amounts));
    }

    [Fact]
    public void SplitEqually_NegativeAmount_DiscountLine_SplitsNegativeSharesExactly()
    {
        var result = MoneySplitter.SplitEqually(-100, [Kevin, Wesley, Jos]);

        Assert.Equal(-100, result.Values.Sum());
        Assert.All(result.Values, v => Assert.True(v <= 0));
    }

    [Fact]
    public void SplitByWeights_UnequalWeights_DistributesProportionally()
    {
        // Kevin pays for 2 units, Wesley for 1 unit of a shared 30 EUR product.
        var result = MoneySplitter.SplitByWeights(3000, [(Kevin, 2m), (Wesley, 1m)]);

        Assert.Equal(2000, result[Kevin]);
        Assert.Equal(1000, result[Wesley]);
    }

    [Fact]
    public void SplitEqually_NoPeople_Throws()
    {
        Assert.Throws<MoneySplitException>(() => MoneySplitter.SplitEqually(1000, []));
    }
}
