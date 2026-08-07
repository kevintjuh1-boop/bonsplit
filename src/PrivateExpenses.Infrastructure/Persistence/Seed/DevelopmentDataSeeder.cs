using Microsoft.EntityFrameworkCore;
using PrivateExpenses.Domain.Calculations;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Infrastructure.Persistence.Seed;

/// <summary>
/// Adds a handful of realistic sample expenses so the dashboard, balances and search screens have
/// something to show. Only intended for local development — never runs against a database that
/// already has real expenses in it, and is gated behind an explicit config flag
/// (<c>SeedDemoData</c>) so a production/real-data run stays empty by default.
/// </summary>
public static class DevelopmentDataSeeder
{
    public static async Task SeedDemoExpensesAsync(PrivateExpensesDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Expenses.AnyAsync(cancellationToken))
        {
            return;
        }

        var people = await context.People.ToListAsync(cancellationToken);
        var kevin = people.Single(p => p.Name == "Kevin");
        var wesley = people.Single(p => p.Name == "Wesley");
        var jos = people.Single(p => p.Name == "Jos");

        var boodschappen = await context.Categories.SingleAsync(c => c.Name == "Boodschappen", cancellationToken);
        var winkelen = await context.Categories.SingleAsync(c => c.Name == "Winkelen", cancellationToken);
        var uitgaan = await context.Categories.SingleAsync(c => c.Name == "Uitgaan", cancellationToken);

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.Today);

        var jumbo = BuildJumboReceipt(kevin, wesley, jos, boodschappen, today, now);
        var albertHeijn = BuildAlbertHeijnReceipt(kevin, wesley, jos, boodschappen, today.AddDays(-1), now);
        var bolCom = BuildManualExpense(
            "Bol.com", winkelen, jos, [jos], 2995, today.AddDays(-5), now, "Handmatig ingevoerd, geen bon.");
        var restaurant = BuildManualExpense(
            "Restaurant De Kroon", uitgaan, wesley, [kevin, wesley, jos], 8760, today.AddDays(-3), now, null);

        context.Expenses.AddRange(jumbo, albertHeijn, bolCom, restaurant);

        // A partial settlement so the "Saldi" and settlement history screens have example data too.
        context.Settlements.Add(new Settlement
        {
            Id = Guid.NewGuid(),
            FromPersonId = wesley.Id,
            ToPersonId = kevin.Id,
            AmountCents = 1500,
            SettlementDate = today.AddDays(-2),
            Note = "Alvast een deel terugbetaald",
            CreatedAt = now,
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Expense BuildJumboReceipt(Person kevin, Person wesley, Person jos, Category category, DateOnly date, DateTime now)
    {
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            MerchantName = "Jumbo",
            ExpenseDate = date,
            CategoryId = category.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var sortOrder = 0;
        AddItem(expense, "Melk 1L", 129, [kevin.Id], ref sortOrder);
        AddItem(expense, "Brood volkoren", 249, [kevin.Id], ref sortOrder);
        AddItem(expense, "Kaas 48+ 500g", 695, [wesley.Id], ref sortOrder);
        AddItem(expense, "Cola 1.5L", 229, [wesley.Id], ref sortOrder);
        AddItem(expense, "Chips paprika", 189, [jos.Id], ref sortOrder);
        AddItem(expense, "Koffie snelfiltermaling", 449, [jos.Id], ref sortOrder);
        AddItem(expense, "Wasmiddel 1.5L", 899, [kevin.Id, wesley.Id], ref sortOrder);
        AddItem(expense, "Toiletpapier 24 rol", 999, [kevin.Id, wesley.Id, jos.Id], ref sortOrder);
        AddItem(expense, "Groentepakket seizoen", 749, [kevin.Id, wesley.Id, jos.Id], ref sortOrder);
        AddItem(expense, "Diepvriespizza 3-pack", 597, [kevin.Id, wesley.Id, jos.Id], ref sortOrder);
        AddItem(expense, "Yoghurt halfvol 1L", 329, [kevin.Id, wesley.Id], ref sortOrder);
        AddItem(expense, "Bonuskorting", -150, [kevin.Id, wesley.Id, jos.Id], ref sortOrder, isDiscount: true);

        expense.TotalCents = expense.Items.Sum(i => i.TotalCents);
        expense.Payments.Add(new ExpensePayment { Id = Guid.NewGuid(), PersonId = kevin.Id, AmountCents = expense.TotalCents });

        return expense;
    }

    private static Expense BuildAlbertHeijnReceipt(Person kevin, Person wesley, Person jos, Category category, DateOnly date, DateTime now)
    {
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            MerchantName = "Albert Heijn",
            ExpenseDate = date,
            CategoryId = category.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var sortOrder = 0;
        AddItem(expense, "Statiegeld flessen", 150, [kevin.Id, wesley.Id, jos.Id], ref sortOrder, isDeposit: true);
        AddItem(expense, "Pasta", 179, [kevin.Id, wesley.Id, jos.Id], ref sortOrder);
        AddItem(expense, "Pastasaus", 249, [kevin.Id, wesley.Id, jos.Id], ref sortOrder);
        AddItem(expense, "Salade", 299, [wesley.Id], ref sortOrder);
        AddItem(expense, "Appelsap 1L", 165, [wesley.Id, jos.Id], ref sortOrder);

        expense.TotalCents = expense.Items.Sum(i => i.TotalCents);
        expense.Payments.Add(new ExpensePayment { Id = Guid.NewGuid(), PersonId = wesley.Id, AmountCents = expense.TotalCents });

        return expense;
    }

    private static Expense BuildManualExpense(
        string merchant, Category category, Person payer, IReadOnlyList<Person> participants,
        long totalCents, DateOnly date, DateTime now, string? notes)
    {
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            MerchantName = merchant,
            ExpenseDate = date,
            CategoryId = category.Id,
            Notes = notes,
            CreatedAt = now,
            UpdatedAt = now,
            TotalCents = totalCents,
        };

        var item = new ExpenseItem
        {
            Id = Guid.NewGuid(),
            Description = merchant,
            Quantity = 1m,
            TotalCents = totalCents,
            SortOrder = 0,
            CreatedAt = now,
        };

        var shares = MoneySplitter.SplitEqually(totalCents, participants.Select(p => p.Id).ToList());
        foreach (var (personId, amount) in shares)
        {
            item.Shares.Add(new ExpenseItemShare { Id = Guid.NewGuid(), PersonId = personId, AmountCents = amount });
        }

        expense.Items.Add(item);
        expense.Payments.Add(new ExpensePayment { Id = Guid.NewGuid(), PersonId = payer.Id, AmountCents = totalCents });

        return expense;
    }

    private static void AddItem(
        Expense expense, string description, long totalCents, IReadOnlyList<Guid> participantIds, ref int sortOrder,
        bool isDiscount = false, bool isDeposit = false)
    {
        var item = new ExpenseItem
        {
            Id = Guid.NewGuid(),
            Description = description,
            Quantity = 1m,
            TotalCents = totalCents,
            SortOrder = sortOrder++,
            IsDiscount = isDiscount,
            IsDeposit = isDeposit,
            CreatedAt = DateTime.UtcNow,
        };

        var shares = MoneySplitter.SplitEqually(totalCents, participantIds);
        foreach (var (personId, amount) in shares)
        {
            item.Shares.Add(new ExpenseItemShare { Id = Guid.NewGuid(), PersonId = personId, AmountCents = amount });
        }

        expense.Items.Add(item);
    }
}
