using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Application.Abstractions.Persistence;

public interface IExternalPaymentRepository
{
    Task<List<ExternalPayment>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ExternalPayment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(ExternalPayment payment, CancellationToken cancellationToken = default);
    Task DeleteAsync(ExternalPayment payment, CancellationToken cancellationToken = default);
}
