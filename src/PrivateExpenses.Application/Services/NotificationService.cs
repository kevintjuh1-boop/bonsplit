using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Application.Abstractions.Services;
using PrivateExpenses.Application.Dtos;

namespace PrivateExpenses.Application.Services;

public class NotificationService(IUnitOfWorkFactory unitOfWorkFactory) : INotificationService
{
    public async Task<List<NotificationDto>> GetForPersonAsync(Guid personId, int count, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var notifications = await uow.Notifications.GetForPersonAsync(personId, count, cancellationToken);
        return notifications
            .Select(n => new NotificationDto(n.Id, n.Message, n.ExpenseId, n.IsRead, n.CreatedAt))
            .ToList();
    }

    public async Task<int> GetUnreadCountAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        return await uow.Notifications.GetUnreadCountAsync(personId, cancellationToken);
    }

    public async Task MarkAllReadAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        await using var uow = await unitOfWorkFactory.CreateAsync(cancellationToken);
        await uow.Notifications.MarkAllReadAsync(personId, cancellationToken);
    }
}
