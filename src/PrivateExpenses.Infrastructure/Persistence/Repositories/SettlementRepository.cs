using Microsoft.EntityFrameworkCore;
using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Infrastructure.Persistence.Repositories;

public class SettlementRepository(PrivateExpensesDbContext context) : ISettlementRepository
{
    public Task<List<Settlement>> GetAllAsync(CancellationToken cancellationToken = default) =>
        context.Settlements.AsNoTracking()
            .Include(s => s.FromPerson)
            .Include(s => s.ToPerson)
            .OrderByDescending(s => s.SettlementDate).ThenByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<List<Settlement>> GetRecentAsync(int count, CancellationToken cancellationToken = default) =>
        context.Settlements.AsNoTracking()
            .Include(s => s.FromPerson)
            .Include(s => s.ToPerson)
            .OrderByDescending(s => s.SettlementDate).ThenByDescending(s => s.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

    public Task<Settlement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Settlements
            .Include(s => s.FromPerson)
            .Include(s => s.ToPerson)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task AddAsync(Settlement settlement, CancellationToken cancellationToken = default) =>
        await context.Settlements.AddAsync(settlement, cancellationToken);

    public Task DeleteAsync(Settlement settlement, CancellationToken cancellationToken = default)
    {
        context.Settlements.Remove(settlement);
        return Task.CompletedTask;
    }
}
