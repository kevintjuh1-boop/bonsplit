namespace PrivateExpenses.Domain.Calculations;

public readonly record struct SuggestedDebt(Guid FromPersonId, Guid ToPersonId, long AmountCents);

/// <summary>
/// Turns per-person net balances (positive = should receive money, negative = owes money) into the
/// smallest practical set of "who pays whom" transactions.
///
/// Algorithm: greedy largest-creditor-vs-largest-debtor matching. Creditors and debtors are each
/// sorted by amount descending (ties broken by Person Id for determinism), then repeatedly the
/// biggest creditor and biggest debtor are matched for min(their remaining amounts) until one side
/// is exhausted. This is not guaranteed to produce the mathematically minimal number of transactions
/// in every case, but it is deterministic and cent-exact.
///
/// In principle every euro paid by someone in the group is owed by someone else in the group, so the
/// balances normally net to zero. They can legitimately drift when an expense was saved despite a
/// confirmed regels/totaal mismatch (the receipt-review page's "sla toch op" override) — real cents
/// that were paid but never attributed to any item or person. Rather than that one messy receipt
/// crashing every balance page for the whole household, any such residual is simply left unmatched
/// once the shorter side runs out, instead of being treated as a hard error.
/// </summary>
public static class DebtSimplifier
{
    public static IReadOnlyList<SuggestedDebt> Simplify(IReadOnlyDictionary<Guid, long> netBalancesCents)
    {
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
