using Microsoft.EntityFrameworkCore;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Infrastructure.Persistence.Seed;

/// <summary>Seeds the three household members and the fixed category list. Safe to run on every
/// startup — each part only inserts data when the corresponding table is empty.</summary>
public static class DbSeeder
{
    /// <summary>The fixed category list, shared with <see cref="Parsing.AnthropicVisionReceiptParser"/>
    /// so its category suggestions always match a name that actually exists in this app.</summary>
    public static readonly (string Name, string IconKey)[] FixedCategories =
    [
        ("Boodschappen", "shopping-cart"),
        ("Eten & drinken", "utensils"),
        ("Wonen", "home"),
        ("Vervoer", "car"),
        ("Uitgaan", "party"),
        ("Abonnementen", "repeat"),
        ("Vakantie", "plane"),
        ("Winkelen", "shopping-bag"),
        ("Gezondheid", "heart-pulse"),
        ("Overig", "more-horizontal"),
    ];

    public static async Task SeedCoreDataAsync(PrivateExpensesDbContext context, CancellationToken cancellationToken = default)
    {
        if (!await context.People.AnyAsync(cancellationToken))
        {
            var now = DateTime.UtcNow;
            // ColorKey values come from a fixed, name-independent palette (see PersonColors.razor.css /
            // the shared palette in app.css) so a renamed or newly added person can still pick a color
            // without the system being hard-wired to "kevin"/"wesley"/"jos". The three values here match
            // the BonSplit brand's fixed Kevin/Wesley/Jos colors (blue/violet/amber).
            context.People.AddRange(
                new Person { Id = Guid.NewGuid(), Name = "Kevin", Initial = "K", ColorKey = "blue", IsActive = true, CreatedAt = now },
                new Person { Id = Guid.NewGuid(), Name = "Wesley", Initial = "W", ColorKey = "violet", IsActive = true, CreatedAt = now },
                new Person { Id = Guid.NewGuid(), Name = "Jos", Initial = "J", ColorKey = "amber", IsActive = true, CreatedAt = now });
        }
        else
        {
            // One-time cosmetic fixup for databases seeded before the BonSplit rebrand, where Wesley
            // was assigned the old "emerald" color instead of the brand's "violet".
            var wesley = await context.People.FirstOrDefaultAsync(p => p.Name == "Wesley" && p.ColorKey == "emerald", cancellationToken);
            if (wesley is not null)
            {
                wesley.ColorKey = "violet";
            }
        }

        if (!await context.Categories.AnyAsync(cancellationToken))
        {
            var sortOrder = 0;
            foreach (var (name, icon) in FixedCategories)
            {
                context.Categories.Add(new Category { Id = Guid.NewGuid(), Name = name, IconKey = icon, SortOrder = sortOrder++ });
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
