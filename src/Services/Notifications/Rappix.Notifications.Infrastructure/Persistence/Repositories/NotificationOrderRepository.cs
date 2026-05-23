using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Domain.NotificationOrders;

namespace Rappix.Notifications.Infrastructure.Persistence.Repositories;

/// <summary>Repositorio de la proyeccion <see cref="NotificationOrder"/>.</summary>
internal sealed class NotificationOrderRepository(NotificationsDbContext db) : INotificationOrderRepository
{
    public async Task<NotificationOrder?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        await db.NotificationOrders.FindAsync([orderId], cancellationToken).ConfigureAwait(false);

    public void Add(NotificationOrder notificationOrder) => db.NotificationOrders.Add(notificationOrder);
}
