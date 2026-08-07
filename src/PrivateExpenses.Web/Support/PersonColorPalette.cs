namespace PrivateExpenses.Web.Support;

/// <summary>Maps a Person's name-independent ColorKey (section 54) to the CSS custom properties
/// defined in app.css, so the same color always shows up everywhere a person appears.</summary>
public static class PersonColorPalette
{
    public static readonly string[] AvailableKeys = ["blue", "emerald", "amber", "rose", "violet", "cyan"];

    private static string Normalize(string colorKey) => AvailableKeys.Contains(colorKey) ? colorKey : "blue";

    public static string AvatarStyle(string colorKey)
    {
        var key = Normalize(colorKey);
        return $"background: var(--person-{key}-soft); color: var(--person-{key});";
    }

    public static string SolidStyle(string colorKey)
    {
        var key = Normalize(colorKey);
        return $"background: var(--person-{key}); color: white;";
    }

    public static string TextStyle(string colorKey)
    {
        var key = Normalize(colorKey);
        return $"color: var(--person-{key});";
    }

    public static string SoftBackgroundVar(string colorKey) => $"var(--person-{Normalize(colorKey)}-soft)";
    public static string SolidVar(string colorKey) => $"var(--person-{Normalize(colorKey)})";
}
