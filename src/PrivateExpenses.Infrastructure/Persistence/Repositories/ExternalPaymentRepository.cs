using Microsoft.EntityFrameworkCore;
using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Infrastructure.Persistence.Repositories;

public class ExternalPaymentRepository(PrivateExpensesDbContext context) : IExternalPaymentRepository
{
    public Task<List<ExternalPayment>> GetAllAsync(CancellationToken cancellationToken = default) =>
        context.ExternalPayments.AsNoTracking()
            .Include(p => p.OwedToPerson)
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<ExternalPayment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.ExternalPayments.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task AddAsync(ExternalPayment payment, CancellationToken cancellationToken = default) =>
        await context.ExternalPayments.AddAsync(payment, cancellationToken);

    public Task DeleteAsync(ExternalPayment payment, CancellationToken cancellationToken = default)
    {
        context.ExternalPayments.Remove(payment);
        return Task.CompletedTask;
    }
}
