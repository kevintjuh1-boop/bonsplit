namespace PrivateExpenses.Web.Support;

/// <summary>Maps a category's icon key to a soft background color for its list-row icon circle,
/// purely a display concern — categories don't store a color themselves.</summary>
public static class CategoryColorPalette
{
    public static string BackgroundStyle(string? iconKey) => iconKey switch
    {
        "shopping-cart" => "background: var(--person-emerald-soft); color: var(--person-emerald);",
        "utensils" => "background: var(--person-amber-soft); color: var(--person-amber);",
        "home" => "background: var(--person-blue-soft); color: var(--person-blue);",
        "car" => "background: var(--person-violet-soft); color: var(--person-violet);",
        "party" => "background: var(--person-rose-soft); color: var(--person-rose);",
        "repeat" => "background: var(--person-cyan-soft); color: var(--person-cyan);",
        "plane" => "background: var(--color-mint-soft); color: var(--color-mint-text);",
        "shopping-bag" => "background: var(--person-rose-soft); color: var(--person-rose);",
        "heart-pulse" => "background: var(--color-danger-soft); color: var(--color-danger-text);",
        _ => "background: var(--color-surface-muted); color: var(--color-text-muted);",
    };
}
