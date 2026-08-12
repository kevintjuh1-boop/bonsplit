using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Application.Abstractions.Persistence;

public interface INotificationRepository
{
    Task<List<Notification>> GetForPersonAsync(Guid personId, int count, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid personId, CancellationToken cancellationToken = default);
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(Guid personId, CancellationToken cancellationToken = default);
}
