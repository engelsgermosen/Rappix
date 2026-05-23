namespace Rappix.Notifications.Application.Abstractions;

/// <summary>
/// Contenido renderizado de una notificacion (subject + body en texto plano). Lo devuelven los
/// metodos de <c>NotificationTemplates</c> para que <c>NotifyHandler</c> lo pase tanto al canal
/// (para enviar) como al aggregate <c>Notification</c> (para persistir el preview).
/// </summary>
/// <param name="Subject">Asunto del email (corto, va al header). Maximo 256 chars idealmente.</param>
/// <param name="Body">Cuerpo del email en texto plano (no HTML — eso es follow-up).</param>
public sealed record NotificationContent(string Subject, string Body);
