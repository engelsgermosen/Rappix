using Rappix.Notifications.Domain.Notifications;

namespace Rappix.Notifications.Application.Notifications;

/// <summary>
/// Destinatario resuelto de una notificacion: UserId + email + nombre (snapshot del momento) + rol.
/// El consumer resuelve estos campos via las proyecciones <c>UserContact</c> y <c>MerchantContact</c>
/// y se los pasa al <see cref="INotifyHandler"/> en el <see cref="NotifyRequest"/>.
/// </summary>
public sealed record Recipient(Guid UserId, string Email, string Name, RecipientRole Role);
