using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Application.Abstractions.Persistence;

public interface IPersonRepository
{
    Task<List<Person>> GetAllAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<Person?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Person person, CancellationToken cancellationToken = default);
}
