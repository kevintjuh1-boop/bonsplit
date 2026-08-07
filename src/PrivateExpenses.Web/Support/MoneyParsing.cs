using System.Globalization;

namespace PrivateExpenses.Web.Support;

/// <summary>Parses a Dutch-formatted amount ("12,34" or "1.234,56") typed into a plain text input
/// into integer cents. UI-only concern — the actual split/validation math never touches this.</summary>
public static class MoneyParsing
{
    public static long? ParseToCents(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var normalized = text.Trim().Replace(".", "").Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return (long)Math.Round(value * 100, MidpointRounding.AwayFromZero);
    }
}
