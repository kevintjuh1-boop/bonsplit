using System.Globalization;

namespace PrivateExpenses.Domain.Money;

/// <summary>Central place for turning integer cent amounts into Dutch/EUR display strings.
/// Never format money ad hoc elsewhere in the codebase.</summary>
public static class MoneyFormatter
{
    public static readonly CultureInfo DutchCulture = CultureInfo.GetCultureInfo("nl-NL");

    public static string Format(long cents)
    {
        var euros = cents / 100m;
        return string.Create(DutchCulture, $"€ {euros.ToString("N2", DutchCulture)}");
    }

    /// <summary>Formats a balance with an explicit +/- sign so "who owes whom" is never ambiguous.</summary>
    public static string FormatSigned(long cents)
    {
        var sign = cents > 0 ? "+" : cents < 0 ? "-" : "";
        return $"{sign}{Format(Math.Abs(cents))}";
    }
}
