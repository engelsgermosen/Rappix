namespace Rappix.Notifications.Application.Abstractions;

/// <summary>
/// Datos minimos que un <see cref="INotificationChannel"/> necesita para enviar una notificacion
/// concreta (snapshot del momento del envio — si el email del usuario cambia despues, el historial
/// persistido preserva el valor original). Independiente del medio: para email Fase 9 son los 4
/// campos; canales futuros (push, SMS) podrian sub-clasificar o usar campos opcionales.
/// </summary>
/// <param name="ToEmail">Email destino — debe estar validado por el caller (NotifyHandler).</param>
/// <param name="ToName">Nombre del destinatario para el header <c>To</c> del email.</param>
/// <param name="Subject">Asunto del email.</param>
/// <param name="Body">Cuerpo del email (texto plano en Fase 9).</param>
public sealed record NotificationMessage(
    string ToEmail,
    string ToName,
    string Subject,
    string Body);

/// <summary>
/// Resultado exitoso de <see cref="INotificationChannel.SendAsync"/>. El
/// <see cref="ProviderMessageId"/> es nullable porque no todos los canales lo exponen — el
/// <c>FakeNotificationChannel</c> devuelve null; <c>SendGridNotificationChannel</c> devuelve el
/// header <c>X-Message-Id</c> si esta presente en la respuesta.
/// </summary>
/// <param name="ProviderMessageId">Id del mensaje en el proveedor (e.g. SendGrid X-Message-Id). Null si el canal no lo expone.</param>
public sealed record NotificationSendResult(string? ProviderMessageId);
