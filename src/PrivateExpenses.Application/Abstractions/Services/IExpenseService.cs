using PrivateExpenses.Application.Dtos;

namespace PrivateExpenses.Application.Abstractions.Services;

public interface IExpenseService
{
    Task<ExpenseDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<ExpenseListItemDto>> GetListAsync(ExpenseFilter filter, CancellationToken cancellationToken = default);
    Task<List<ExpenseListItemDto>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
    Task<long> GetMonthTotalCentsAsync(DateOnly monthStart, CancellationToken cancellationToken = default);
    Task<long> GetMonthSavingsCentsAsync(DateOnly monthStart, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(CreateExpenseRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateManualExpenseAsync(ManualExpenseRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid expenseId, CreateExpenseRequest request, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(Guid expenseId, CancellationToken cancellationToken = default);
}
