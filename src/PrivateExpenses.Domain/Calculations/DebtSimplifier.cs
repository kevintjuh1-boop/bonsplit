using PrivateExpenses.Domain.Exceptions;

namespace PrivateExpenses.Domain.Calculations;

public readonly record struct SuggestedDebt(Guid FromPersonId, Guid ToPersonId, long AmountCents);

/// <summary>
/// Turns per-person net balances (positive = should receive money, negative = owes money) into the
/// smallest practical set of "who pays whom" transactions.
///
/// Algorithm: greedy largest-creditor-vs-largest-debtor matching. Creditors and debtors are each
/// sorted by amount descending (ties broken by Person Id for determinism), then repeatedly the
/// biggest creditor and biggest debtor are matched for min(their remaining amounts) until both sides
/// are exhausted. This is not guaranteed to produce the mathematically minimal number of transactions
/// in every case, but it is deterministic, cent-exact, and always financially equivalent to the netted
/// balances — the same input always simplifies to the same output.
/// </summary>
public static class DebtSimplifier
{
    public static IReadOnlyList<SuggestedDebt> Simplify(IReadOnlyDictionary<Guid, long> netBalancesCents)
    {
        var sum = netBalancesCents.Values.Sum();
        if (sum != 0)
        {
            throw new DomainException(
                $"Saldi moeten optellen tot 0, maar tellen op tot {sum} cent. Er is een fout in de saldoberekening.");
        }

        var creditors = netBalancesCents
            .Where(kv => kv.Value > 0)
            .Select(kv => (Id: kv.Key, Remaining: kv.Value))
            .OrderByDescending(x => x.Remaining)
            .ThenBy(x => x.Id)
            .ToList();

        var debtors = netBalancesCents
            .Where(kv => kv.Value < 0)
            .Select(kv => (Id: kv.Key, Remaining: -kv.Value))
            .OrderByDescending(x => x.Remaining)
            .ThenBy(x => x.Id)
            .ToList();

        var transactions = new List<SuggestedDebt>();
        var i = 0;
        var j = 0;

        while (i < creditors.Count && j < debtors.Count)
        {
            var creditor = creditors[i];
            var debtor = debtors[j];
            var amount = Math.Min(creditor.Remaining, debtor.Remaining);

            if (amount > 0)
            {
                transactions.Add(new SuggestedDebt(debtor.Id, creditor.Id, amount));
            }

            creditors[i] = (creditor.Id, creditor.Remaining - amount);
            debtors[j] = (debtor.Id, debtor.Remaining - amount);

            if (creditors[i].Remaining == 0)
            {
                i++;
            }

            if (debtors[j].Remaining == 0)
            {
                j++;
            }
        }

        return transactions;
    }
}
