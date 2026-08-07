using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Application.Abstractions.Services;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Application.Services;

public class CategoryService(IUnitOfWorkFactory unitOfWorkFactory) : ICategoryService
{
    public async Task<List<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        return await uow.Categories.GetAllAsync(cancellationToken);
    }
}
