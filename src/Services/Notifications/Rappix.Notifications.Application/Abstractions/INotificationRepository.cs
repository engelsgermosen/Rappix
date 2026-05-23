using Rappix.Notifications.Domain.Notifications;

namespace Rappix.Notifications.Application.Abstractions;

/// <summary>
/// Repositorio del aggregate <see cref="Notification"/>. El NotifyHandler hace
/// <see cref="GetByBusinessKeyAsync"/> ANTES de insertar para el dedup de primer nivel; si pierde
/// el race contra otro worker, el unique partial index <c>UX_Notification_BusinessKey</c> sobre
/// <c>(RelatedOrderId, RecipientUserId, NotificationType)</c> bloquea el segundo insert y la
/// infraestructura lanza <see cref="DuplicateNotificationException"/> que el handler trata como
/// no-op idempotente.
/// </summary>
public interface INotificationRepository
{
    /// <summary>
    /// Busca una notificacion existente por la clave de negocio (no por <c>SourceMessageId</c> — eso
    /// es informacional). Devuelve null si no existe. Solo valido cuando <paramref name="relatedOrderId"/>
    /// no es null (las notificaciones no-pedido se dedupen por otros mecanismos no soportados en Fase 9).
    /// </summary>
    Task<Notification?> GetByBusinessKeyAsync(
        Guid relatedOrderId,
        Guid recipientUserId,
        NotificationType notificationType,
        CancellationToken cancellationToken);

    /// <summary>Lo marca para insercion al proximo SaveChanges.</summary>
    void Add(Notification notification);
}
