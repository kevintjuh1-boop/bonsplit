using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Application.Abstractions.Persistence;

public interface ISettlementRepository
{
    Task<List<Settlement>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<Settlement>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    Task AddAsync(Settlement settlement, CancellationToken cancellationToken = default);
}
