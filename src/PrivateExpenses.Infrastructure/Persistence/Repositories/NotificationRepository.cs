using Microsoft.EntityFrameworkCore;
using PrivateExpenses.Application.Abstractions.Persistence;
using PrivateExpenses.Domain.Entities;

namespace PrivateExpenses.Infrastructure.Persistence.Repositories;

public class NotificationRepository(PrivateExpensesDbContext context) : INotificationRepository
{
    public Task<List<Notification>> GetForPersonAsync(Guid personId, int count, CancellationToken cancellationToken = default) =>
        context.Notifications.AsNoTracking()
            .Where(n => n.RecipientPersonId == personId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);

    public Task<int> GetUnreadCountAsync(Guid personId, CancellationToken cancellationToken = default) =>
        context.Notifications.AsNoTracking()
            .Where(n => n.RecipientPersonId == personId && !n.IsRead)
            .CountAsync(cancellationToken);

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default) =>
        await context.Notifications.AddAsync(notification, cancellationToken);

    public Task MarkAllReadAsync(Guid personId, CancellationToken cancellationToken = default) =>
        context.Notifications
            .Where(n => n.RecipientPersonId == personId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true), cancellationToken);
}
