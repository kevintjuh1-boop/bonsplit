using Microsoft.EntityFrameworkCore;
using PrivateExpenses.Application.Abstractions.Persistence;

namespace PrivateExpenses.Infrastructure.Persistence;

public class EfUnitOfWorkFactory(IDbContextFactory<PrivateExpensesDbContext> contextFactory) : IUnitOfWorkFactory
{
    public async Task<IUnitOfWork> CreateAsync(CancellationToken cancellationToken = default)
    {
        var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return new EfUnitOfWork(context);
    }
}
