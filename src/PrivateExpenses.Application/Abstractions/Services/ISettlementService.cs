using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Application.Abstractions.Services;

public interface ISettlementService
{
    Task<List<Settlement>> GetHistoryAsync(CancellationToken cancellationToken = default);
    Task<List<Settlement>> GetRecentAsync(int count, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(Guid fromPersonId, Guid toPersonId, long amountCents, DateOnly date, string? note, CancellationToken cancellationToken = default);
}
