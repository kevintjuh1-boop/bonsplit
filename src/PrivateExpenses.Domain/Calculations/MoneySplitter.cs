using PrivateExpenses.Domain.Exceptions;

namespace PrivateExpenses.Domain.Calculations;

/// <summary>
/// Cent-exact splitting of a money amount across people. Every method here guarantees the resulting
/// shares sum to exactly the input amount — no cents may be lost or invented to rounding.
///
/// Rounding strategy: largest-remainder (Hamilton) apportionment. Each person's exact (fractional)
/// share is computed, floored, and any leftover cents are handed out one at a time to the people with
/// the largest fractional remainder. Ties (e.g. an equal split, where every remainder is identical) are
/// broken by the order of <c>personIdsInOrder</c> as supplied by the caller — callers should pass a
/// stable order (e.g. people sorted by Id or Name) so results are reproducible for the same input.
/// </summary>
public static class MoneySplitter
{
    public static IReadOnlyDictionary<Guid, long> SplitEqually(long totalCents, IReadOnlyList<Guid> personIdsInOrder)
    {
        if (personIdsInOrder.Count == 0)
        {
            throw new MoneySplitException("Er moet minstens één persoon zijn om een bedrag te verdelen.");
        }

        var weights = personIdsInOrder.Select(id => (PersonId: id, Weight: 1m)).ToList();
        return SplitByWeights(totalCents, weights);
    }

    public static IReadOnlyDictionary<Guid, long> SplitByPercentages(
        long totalCents,
        IReadOnlyList<(Guid PersonId, decimal Percentage)> percentagesInOrder)
    {
        if (percentagesInOrder.Count == 0)
        {
            throw new MoneySplitException("Er moet minstens één persoon zijn om een bedrag te verdelen.");
        }

        var sum = percentagesInOrder.Sum(p => p.Percentage);
        if (Math.Abs(sum - 100m) > 0.01m)
        {
            throw new MoneySplitException($"Percentages moeten optellen tot 100%, maar tellen op tot {sum}%.");
        }

        var weights = percentagesInOrder.Select(p => (p.PersonId, Weight: p.Percentage)).ToList();
        return SplitByWeights(totalCents, weights);
    }

    public static IReadOnlyDictionary<Guid, long> SplitByWeights(
        long totalCents,
        IReadOnlyList<(Guid PersonId, decimal Weight)> weightsInOrder)
    {
        if (weightsInOrder.Count == 0)
        {
            throw new MoneySplitException("Er moet minstens één persoon zijn om een bedrag te verdelen.");
        }

        var totalWeight = weightsInOrder.Sum(w => w.Weight);
        if (totalWeight <= 0)
        {
            throw new MoneySplitException("De verdeling moet minstens één positief gewicht bevatten.");
        }

        // Negative totals (e.g. discount lines) are split with the same magnitude logic; the sign
        // just carries through consistently for every participant.
        var sign = totalCents < 0 ? -1 : 1;
        var absTotal = Math.Abs(totalCents);

        var exactShares = weightsInOrder
            .Select(w => absTotal * w.Weight / totalWeight)
            .ToList();

        var floorShares = exactShares.Select(s => (long)Math.Floor(s)).ToList();
        var distributed = floorShares.Sum();
        var remainder = (int)(absTotal - distributed);

        var remainders = exactShares
            .Select((exact, index) => (index, fraction: exact - floorShares[index]))
            .OrderByDescending(x => x.fraction)
            .ThenBy(x => x.index) // stable tie-break: earliest in caller-supplied order wins the extra cent
            .ToList();

        var result = new long[weightsInOrder.Count];
        for (var i = 0; i < floorShares.Count; i++)
        {
            result[i] = floorShares[i];
        }

        for (var i = 0; i < remainder; i++)
        {
            result[remainders[i].index] += 1;
        }

        var shares = new Dictionary<Guid, long>();
        for (var i = 0; i < weightsInOrder.Count; i++)
        {
            shares[weightsInOrder[i].PersonId] = sign * result[i];
        }

        return shares;
    }

    /// <summary>Validates a caller-supplied custom split (exact amounts or amounts already converted
    /// from percentages) sums to exactly the expected total. Throws rather than silently correcting.</summary>
    public static void ValidateExactSplit(long expectedTotalCents, IReadOnlyDictionary<Guid, long> amounts)
    {
        if (amounts.Count == 0)
        {
            throw new MoneySplitException("Er moet minstens één persoon een aandeel hebben.");
        }

        var sum = amounts.Values.Sum();
        if (sum != expectedTotalCents)
        {
            throw new MoneySplitException(
                $"De som van de verdeling ({sum} cent) komt niet exact overeen met het bedrag ({expectedTotalCents} cent).");
        }
    }
}
