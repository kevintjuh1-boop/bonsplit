using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Application.Abstractions.Persistence;

public interface ICategoryRepository
{
    Task<List<Category>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
