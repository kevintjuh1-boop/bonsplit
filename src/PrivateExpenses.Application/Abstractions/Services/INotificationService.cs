using PrivateExpenses.Application.Dtos;

namespace PrivateExpenses.Application.Abstractions.Services;

public interface INotificationService
{
    Task<List<NotificationDto>> GetForPersonAsync(Guid personId, int count, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid personId, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(Guid personId, CancellationToken cancellationToken = default);
}
