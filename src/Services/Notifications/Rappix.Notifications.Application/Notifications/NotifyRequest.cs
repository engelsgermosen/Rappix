using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Domain.Notifications;

namespace Rappix.Notifications.Application.Notifications;

/// <summary>
/// Solicitud de envio que un consumer construye y entrega al <see cref="INotifyHandler"/>. El
/// handler se encarga de: (1) idempotency lookup por clave de negocio, (2) insert <c>Pending</c>
/// + SaveChanges (que puede lanzar <see cref="Abstractions.DuplicateNotificationException"/> y ser
/// tratado como no-op), (3) llamada al canal, (4) MarkSent/MarkFailed + SaveChanges.
/// </summary>
/// <param name="SourceMessageId">
/// MessageId del integration event del broker que produjo este envio. INFORMACIONAL — se persiste
/// en <c>Notification.SourceMessageId</c> para auditoria pero NO participa del dedup (el dedup es
/// por la clave de negocio, ADR-0010 D4).
/// </param>
/// <param name="Recipient">Destinatario resuelto (email + nombre + rol).</param>
/// <param name="Type">Tipo logico de la notificacion (NewOrder, OrderDelivered, etc.).</param>
/// <param name="RelatedOrderId">Pedido al que pertenece — clave de dedup en Fase 9.</param>
/// <param name="Content">Subject + Body renderizados por <c>NotificationTemplates</c>.</param>
public sealed record NotifyRequest(
    string? SourceMessageId,
    Recipient Recipient,
    NotificationType Type,
    Guid RelatedOrderId,
    NotificationContent Content);
