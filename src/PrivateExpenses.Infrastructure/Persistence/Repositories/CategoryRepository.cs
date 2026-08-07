using Microsoft.EntityFrameworkCore;
using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Infrastructure.Persistence.Repositories;

public class CategoryRepository(PrivateExpensesDbContext context) : ICategoryRepository
{
    public Task<List<Category>> GetAllAsync(CancellationToken cancellationToken = default) =>
        context.Categories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync(cancellationToken);

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
}
