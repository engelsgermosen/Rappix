using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Notifications.Application.Abstractions;

namespace Rappix.Notifications.Infrastructure.Channels;

/// <summary>
/// Implementacion in-process de <see cref="INotificationChannel"/> que NO envia emails reales —
/// solo loguea (a Seq via LoggerMessage source-gen) los 4 campos del <see cref="NotificationMessage"/>
/// como un bloque legible. Es el canal por defecto (<c>Notifications:Channel=Fake</c>) y el que
/// usa el smoke E2E + todos los tests integration sin requerir cuenta SendGrid ni red.
/// </summary>
/// <remarks>
/// Devuelve siempre <see cref="Result.Success{TValue}(TValue)"/> con <c>ProviderMessageId=null</c>
/// (no hay proveedor). No mantiene estado (a diferencia de FakePaymentGateway que usa
/// <c>ConcurrentDictionary</c> para simular la idempotency cache de Stripe — aqui el dedup vive en
/// el aggregate y el unique index de BD, no en el canal). Singleton: el log no requiere scope.
/// </remarks>
internal sealed partial class FakeNotificationChannel(ILogger<FakeNotificationChannel> logger)
    : INotificationChannel
{
    public Task<Result<NotificationSendResult>> SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Log a Information con TODOS los campos (subject + body completo) — la evidencia del smoke
        // E2E sale de aqui: en Seq se ve "Notifications.Fake] Sent type=... to=... subject=...".
        LogFakeSent(logger, message.ToName, message.ToEmail, message.Subject, message.Body);

        return Task.FromResult(Result.Success(new NotificationSendResult(ProviderMessageId: null)));
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[Notifications.Fake] Sent to=\"{ToName}\" <{ToEmail}>\n  Subject: {Subject}\n  Body:\n{Body}")]
    private static partial void LogFakeSent(ILogger logger, string toName, string toEmail, string subject, string body);
}
