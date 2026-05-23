using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Notifications.Application.Abstractions;

/// <summary>
/// Adaptador hacia un canal de envio de notificaciones (email en Fase 9; push/SMS son follow-up).
/// Dos implementaciones conmutables por config <c>Notifications:Channel</c>:
/// <c>"Fake"</c> (default; log-only, no envia nada real) y <c>"SendGrid"</c> (envia via SendGrid).
/// El dominio jamas referencia <c>SendGrid</c> directamente — toda interaccion pasa por esta
/// interfaz (Hexagonal/Ports-and-Adapters, mirror de <c>IPaymentGateway</c> en Payments). Cambiar
/// a otro proveedor (Mailgun, SES) es swap del adaptador en Infrastructure, cero cambio aqui.
/// </summary>
public interface INotificationChannel
{
    /// <summary>
    /// Envia una notificacion. Devuelve <see cref="Result{TValue}"/> de exito con el
    /// <c>ProviderMessageId</c> opcional, o fallo con un <c>Error</c> tipado (e.g.
    /// <c>"Notifications.Channel.SendFailed"</c>). NO debe lanzar excepciones — el canal captura
    /// cualquier excepcion interna y la devuelve como <see cref="Result.Failure(Error)"/>. Esto
    /// permite al <c>NotifyHandler</c> persistir la notificacion como <c>Failed</c> sin desencadenar
    /// retry del broker (los retries de envio son responsabilidad del job de reintento, follow-up).
    /// </summary>
    Task<Result<NotificationSendResult>> SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken);
}
