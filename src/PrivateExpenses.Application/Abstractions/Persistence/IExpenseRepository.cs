using PrivateExpenses.Application.Dtos;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Application.Abstractions.Persistence;

public interface IExpenseRepository
{
    /// <summary>Loads a single expense with items, shares, payments, category and documents for
    /// detail/edit screens.</summary>
    Task<Expense?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<ExpenseListItemDto>> GetListAsync(ExpenseFilter filter, CancellationToken cancellationToken = default);

    Task<List<ExpenseListItemDto>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>Person -> total cents owed for their item shares, across all non-deleted expenses.
    /// Used by the balance engine; deliberately projects instead of loading full item graphs.</summary>
    Task<Dictionary<Guid, long>> GetTotalOwedPerPersonAsync(CancellationToken cancellationToken = default);

    /// <summary>Person -> total cents paid via ExpensePayment, across all non-deleted expenses.</summary>
    Task<Dictionary<Guid, long>> GetTotalPaidPerPersonAsync(CancellationToken cancellationToken = default);

    Task<long> GetTotalCentsThisMonthAsync(DateOnly monthStart, DateOnly monthEndExclusive, CancellationToken cancellationToken = default);

    /// <summary>Sum of all discount item lines (stored as negative cents) within the date range, across
    /// non-deleted expenses — returned as a positive amount representing money saved.</summary>
    Task<long> GetTotalSavedFromDiscountsAsync(DateOnly rangeStart, DateOnly rangeEndExclusive, CancellationToken cancellationToken = default);

    /// <summary>Loads filtered expenses with the item/share/payment graph needed for CSV export, in
    /// one query rather than one per expense.</summary>
    Task<List<Expense>> GetForExportAsync(ExpenseFilter filter, CancellationToken cancellationToken = default);

    Task AddAsync(Expense expense, CancellationToken cancellationToken = default);

    void Update(Expense expense);
}
