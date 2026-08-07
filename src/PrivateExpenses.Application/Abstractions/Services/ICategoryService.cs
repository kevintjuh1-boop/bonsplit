using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Application.Abstractions.Services;

public interface ICategoryService
{
    Task<List<Category>> GetAllAsync(CancellationToken cancellationToken = default);
}
