using Microsoft.EntityFrameworkCore;
using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Application.Dtos;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Infrastructure.Persistence.Repositories;

public class ExpenseRepository(PrivateExpensesDbContext context) : IExpenseRepository
{
    public Task<Expense?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Expenses
            .Include(e => e.Category)
            .Include(e => e.Items).ThenInclude(i => i.Shares).ThenInclude(s => s.Person)
            .Include(e => e.Payments).ThenInclude(p => p.Person)
            .Include(e => e.Documents)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<List<ExpenseListItemDto>> GetListAsync(ExpenseFilter filter, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(context.Expenses.AsNoTracking().Where(e => !e.IsDeleted), filter);

        query = filter.Sort switch
        {
            ExpenseFilter.SortOption.DateAscending => query.OrderBy(e => e.ExpenseDate).ThenBy(e => e.CreatedAt),
            ExpenseFilter.SortOption.AmountDescending => query.OrderByDescending(e => e.TotalCents),
            ExpenseFilter.SortOption.AmountAscending => query.OrderBy(e => e.TotalCents),
            _ => query.OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.CreatedAt),
        };

        return await ProjectToListItem(query).ToListAsync(cancellationToken);
    }

    public Task<List<ExpenseListItemDto>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        var query = context.Expenses.AsNoTracking()
            .Where(e => !e.IsDeleted)
            .OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.CreatedAt)
            .Take(count);

        return ProjectToListItem(query).ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, long>> GetTotalOwedPerPersonAsync(CancellationToken cancellationToken = default)
    {
        return await context.ExpenseItemShares
            .Where(s => !s.ExpenseItem!.Expense!.IsDeleted)
            .GroupBy(s => s.PersonId)
            .Select(g => new { PersonId = g.Key, Total = g.Sum(s => s.AmountCents) })
            .ToDictionaryAsync(x => x.PersonId, x => x.Total, cancellationToken);
    }

    public async Task<Dictionary<Guid, long>> GetTotalPaidPerPersonAsync(CancellationToken cancellationToken = default)
    {
        return await context.ExpensePayments
            .Where(p => !p.Expense!.IsDeleted)
            .GroupBy(p => p.PersonId)
            .Select(g => new { PersonId = g.Key, Total = g.Sum(p => p.AmountCents) })
            .ToDictionaryAsync(x => x.PersonId, x => x.Total, cancellationToken);
    }

    public async Task<long> GetTotalCentsThisMonthAsync(DateOnly monthStart, DateOnly monthEndExclusive, CancellationToken cancellationToken = default)
    {
        var sum = await context.Expenses
            .Where(e => !e.IsDeleted && e.ExpenseDate >= monthStart && e.ExpenseDate < monthEndExclusive)
            .SumAsync(e => (long?)e.TotalCents, cancellationToken);

        return sum ?? 0;
    }

    public async Task<long> GetTotalSavedFromDiscountsAsync(DateOnly rangeStart, DateOnly rangeEndExclusive, CancellationToken cancellationToken = default)
    {
        var sum = await context.ExpenseItems
            .Where(i => i.IsDiscount
                && !i.Expense!.IsDeleted
                && i.Expense.ExpenseDate >= rangeStart
                && i.Expense.ExpenseDate < rangeEndExclusive)
            .SumAsync(i => (long?)i.TotalCents, cancellationToken);

        return Math.Abs(sum ?? 0);
    }

    public async Task<List<Expense>> GetForExportAsync(ExpenseFilter filter, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(context.Expenses.AsNoTracking().Where(e => !e.IsDeleted), filter)
            .Include(e => e.Category)
            .Include(e => e.Items).ThenInclude(i => i.Shares).ThenInclude(s => s.Person)
            .Include(e => e.Payments).ThenInclude(p => p.Person)
            .OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.CreatedAt);

        return await query.ToListAsync(cancellationToken);
    }

    public Task<List<ExternalShareDto>> GetExternalSharesAsync(CancellationToken cancellationToken = default) =>
        context.ExpenseItems.AsNoTracking()
            .Where(i => i.ExternalRecipientName != null && !i.Expense!.IsDeleted)
            .OrderByDescending(i => i.Expense!.ExpenseDate).ThenByDescending(i => i.CreatedAt)
            .Select(i => new ExternalShareDto(
                i.Id, i.ExpenseId, i.ExternalRecipientName!, i.Description,
                i.Expense!.MerchantName, i.Expense.ExpenseDate, i.TotalCents, i.IsExternalSettled, i.ExternalSettledAt))
            .ToListAsync(cancellationToken);

    public Task<ExpenseItem?> GetItemByIdAsync(Guid itemId, CancellationToken cancellationToken = default) =>
        context.ExpenseItems.FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);

    public async Task AddAsync(Expense expense, CancellationToken cancellationToken = default) =>
        await context.Expenses.AddAsync(expense, cancellationToken);

    public void Update(Expense expense) => context.Expenses.Update(expense);

    private static IQueryable<Expense> ApplyFilter(IQueryable<Expense> query, ExpenseFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            // EF Core's Sqlite provider translates string.Contains to instr(), which is case-sensitive.
            // EF.Functions.Like maps to SQLite's LIKE, which is case-insensitive for ASCII by default —
            // required so searching "cola" still finds an item saved as "Cola 1.5L".
            var pattern = $"%{filter.SearchText.Trim()}%";
            query = query.Where(e =>
                EF.Functions.Like(e.MerchantName, pattern) ||
                (e.Notes != null && EF.Functions.Like(e.Notes, pattern)) ||
                e.Items.Any(i => EF.Functions.Like(i.Description, pattern)));
        }

        if (filter.FromDate is { } from)
        {
            query = query.Where(e => e.ExpenseDate >= from);
        }

        if (filter.ToDate is { } to)
        {
            query = query.Where(e => e.ExpenseDate <= to);
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(e => e.CategoryId == categoryId);
        }

        if (filter.PayerPersonId is { } payerId)
        {
            query = query.Where(e => e.Payments.Any(p => p.PersonId == payerId));
        }

        if (filter.InvolvesPersonId is { } personId)
        {
            query = query.Where(e => e.Items.Any(i => i.Shares.Any(s => s.PersonId == personId)));
        }

        if (filter.MinAmountCents is { } min)
        {
            query = query.Where(e => e.TotalCents >= min);
        }

        if (filter.MaxAmountCents is { } max)
        {
            query = query.Where(e => e.TotalCents <= max);
        }

        return query;
    }

    private static IQueryable<ExpenseListItemDto> ProjectToListItem(IQueryable<Expense> query) =>
        query.Select(e => new ExpenseListItemDto(
            e.Id,
            e.MerchantName,
            e.ExpenseDate,
            e.TotalCents,
            e.Category != null ? e.Category.Name : null,
            e.Category != null ? e.Category.IconKey : null,
            e.Payments.Select(p => new PersonSummaryDto(p.Person!.Id, p.Person.Name, p.Person.Initial, p.Person.ColorKey)).ToList(),
            e.Documents.Any()));
}
