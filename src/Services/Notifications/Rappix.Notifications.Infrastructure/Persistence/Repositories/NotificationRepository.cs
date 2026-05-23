using Microsoft.EntityFrameworkCore;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Domain.Notifications;

namespace Rappix.Notifications.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repositorio del aggregate <see cref="Notification"/>. <see cref="GetByBusinessKeyAsync"/> es el
/// lookup de idempotencia de Nivel 1 (ADR-0010 D4): el NotifyHandler lo invoca ANTES de insertar
/// para evitar el round-trip de SaveChanges si la notificacion ya existe. El caso de race lo
/// atrapa el unique index a nivel BD (Nivel 2).
/// </summary>
internal sealed class NotificationRepository(NotificationsDbContext db) : INotificationRepository
{
    public Task<Notification?> GetByBusinessKeyAsync(
        Guid relatedOrderId,
        Guid recipientUserId,
        NotificationType notificationType,
        CancellationToken cancellationToken) =>
        db.Notifications
            .FirstOrDefaultAsync(
                notification => notification.RelatedOrderId == relatedOrderId
                    && notification.RecipientUserId == recipientUserId
                    && notification.NotificationType == notificationType,
                cancellationToken);

    public void Add(Notification notification) => db.Notifications.Add(notification);
}
