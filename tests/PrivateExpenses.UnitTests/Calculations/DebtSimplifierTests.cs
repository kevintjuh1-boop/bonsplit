using PrivateExpenses.Domain.Calculations;

namespace PrivateExpenses.UnitTests.Calculations;

public class DebtSimplifierTests
{
    private static readonly Guid Kevin = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Wesley = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Jos = Guid.Parse("00000000-0000-0000-0000-000000000003");

    [Fact]
    public void Simplify_KevinPaidForWesley_WesleyOwesKevin()
    {
        var balances = new Dictionary<Guid, long> { [Kevin] = 3000, [Wesley] = -3000 };

        var debts = DebtSimplifier.Simplify(balances);

        var debt = Assert.Single(debts);
        Assert.Equal(Wesley, debt.FromPersonId);
        Assert.Equal(Kevin, debt.ToPersonId);
        Assert.Equal(3000, debt.AmountCents);
    }

    [Fact]
    public void Simplify_KevinPaidForWesleyAndJos_BothOweKevin()
    {
        // Bon = 90 EUR, Kevin paid, everyone's share is 30 EUR -> Kevin +60, Wesley -30, Jos -30.
        var balances = new Dictionary<Guid, long> { [Kevin] = 6000, [Wesley] = -3000, [Jos] = -3000 };

        var debts = DebtSimplifier.Simplify(balances);

        Assert.Equal(2, debts.Count);
        Assert.Contains(debts, d => d is { FromPersonId: var f, ToPersonId: var t, AmountCents: 3000 } && f == Wesley && t == Kevin);
        Assert.Contains(debts, d => d is { FromPersonId: var f, ToPersonId: var t, AmountCents: 3000 } && f == Jos && t == Kevin);
    }

    [Fact]
    public void Simplify_NettedMutualDebt_CollapsesToSingleTransaction()
    {
        // Wesley owes Kevin 50, Kevin owes Wesley 20 -> net: Wesley owes Kevin 30.
        var balances = new Dictionary<Guid, long> { [Kevin] = 3000, [Wesley] = -3000 };

        var debts = DebtSimplifier.Simplify(balances);

        var debt = Assert.Single(debts);
        Assert.Equal(Wesley, debt.FromPersonId);
        Assert.Equal(Kevin, debt.ToPersonId);
        Assert.Equal(3000, debt.AmountCents);
    }

    [Fact]
    public void Simplify_ZeroBalances_ProducesNoTransactions()
    {
        var balances = new Dictionary<Guid, long> { [Kevin] = 0, [Wesley] = 0, [Jos] = 0 };

        var debts = DebtSimplifier.Simplify(balances);

        Assert.Empty(debts);
    }

    [Fact]
    public void Simplify_CircularDebtsThatNetToZero_ProducesNoTransactions()
    {
        // Kevin -> Wesley 10, Wesley -> Jos 10, Jos -> Kevin 10: everyone paid and is owed 10, net 0.
        var balances = new Dictionary<Guid, long> { [Kevin] = 0, [Wesley] = 0, [Jos] = 0 };

        var debts = DebtSimplifier.Simplify(balances);

        Assert.Empty(debts);
    }

    [Fact]
    public void Simplify_ThreeWayImbalance_TransactionsAreFinanciallyEquivalent()
    {
        var balances = new Dictionary<Guid, long> { [Kevin] = 5000, [Wesley] = -2000, [Jos] = -3000 };

        var debts = DebtSimplifier.Simplify(balances);

        var netPerPerson = balances.Keys.ToDictionary(id => id, _ => 0L);
        foreach (var debt in debts)
        {
            netPerPerson[debt.FromPersonId] -= debt.AmountCents;
            netPerPerson[debt.ToPersonId] += debt.AmountCents;
        }

        foreach (var (personId, balance) in balances)
        {
            Assert.Equal(balance, netPerPerson[personId]);
        }
    }

    [Fact]
    public void Simplify_IsDeterministic_SameInputAlwaysProducesSameOutput()
    {
        var balances = new Dictionary<Guid, long> { [Kevin] = 5000, [Wesley] = -2000, [Jos] = -3000 };

        var first = DebtSimplifier.Simplify(balances);
        var second = DebtSimplifier.Simplify(balances);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Simplify_BalancesThatDoNotSumToZero_MatchesWhatItCanAndLeavesTheResidualUnmatched()
    {
        // A receipt saved despite a confirmed regels/totaal mismatch can leave real cents unaccounted
        // for. That must never crash the whole household's balance pages — Wesley's €20 debt to Kevin
        // still gets suggested; Kevin's extra unmatched €10 (nobody owes it to him) is just dropped.
        var balances = new Dictionary<Guid, long> { [Kevin] = 3000, [Wesley] = -2000 };

        var result = DebtSimplifier.Simplify(balances);

        var debt = Assert.Single(result);
        Assert.Equal(Wesley, debt.FromPersonId);
        Assert.Equal(Kevin, debt.ToPersonId);
        Assert.Equal(2000, debt.AmountCents);
    }
}
