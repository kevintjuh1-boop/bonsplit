using Microsoft.EntityFrameworkCore;
using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Infrastructure.Persistence.Repositories;

public class PersonRepository(PrivateExpensesDbContext context) : IPersonRepository
{
    public async Task<List<Person>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = context.People.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        return await query.OrderBy(p => p.Name).ToListAsync(cancellationToken);
    }

    public Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.People.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task AddAsync(Person person, CancellationToken cancellationToken = default) =>
        await context.People.AddAsync(person, cancellationToken);
}
