using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Domain.Notifications;

namespace Rappix.Notifications.Application.Notifications;

/// <summary>
/// Implementacion del orquestador <see cref="INotifyHandler"/>. Garantiza idempotencia end-to-end
/// del envio segun los 3 niveles de ADR-0010 D4:
/// <list type="number">
/// <item>Lookup por clave de negocio <c>(RelatedOrderId, RecipientUserId, NotificationType)</c> antes
///   de insertar — cubre el 99% de redeliveries del broker que el inbox EF deja pasar.</item>
/// <item>Insert <c>Pending</c> + SaveChanges DENTRO de la tx del outbox callback — si dos workers
///   pierden el race del lookup, el unique partial index <c>UX_Notification_BusinessKey</c> bloquea
///   el segundo insert; la infraestructura traduce el constraint violation a
///   <see cref="DuplicateNotificationException"/>, que aqui se captura y se trata como no-op.</item>
/// <item>Llamada al canal SOLO si pasaron los dos niveles anteriores — el envio nunca se duplica.</item>
/// </list>
/// Tras el envio, transiciona el aggregate a <see cref="NotificationStatus.Sent"/> o
/// <see cref="NotificationStatus.Failed"/> y persiste. El fallo del canal NO relanza — se persiste
/// como fila <c>Failed</c> con el motivo y queda como candidata a un job de reintento (follow-up).
/// </summary>
internal sealed partial class NotifyHandler(
    INotificationRepository notificationRepository,
    INotificationChannel channel,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<NotifyHandler> logger) : INotifyHandler
{
    public async Task<Result> SendAsync(NotifyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Nivel 1: lookup por clave de negocio. Cubre redelivery donde el inbox EF NO ataja (e.g.
        // dos eventos distintos que mapean a la misma notificacion logica — el clasico Delivered+Completed).
        Notification? existing = await notificationRepository.GetByBusinessKeyAsync(
            relatedOrderId: request.RelatedOrderId,
            recipientUserId: request.Recipient.UserId,
            notificationType: request.Type,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            Guid orderId = request.RelatedOrderId;
            Guid recipientId = request.Recipient.UserId;
            string typeName = request.Type.ToString();
            LogAlreadyNotified(logger, orderId, recipientId, typeName);
            return Result.Success();
        }

        // Construye el aggregate (valida email + subject; trunca body a 512 chars).
        DateTime startedAtUtc = clock.UtcNow;
        Result<Notification> createResult = Notification.Create(
            sourceMessageId: request.SourceMessageId,
            recipientUserId: request.Recipient.UserId,
            recipientEmail: request.Recipient.Email,
            recipientRole: request.Recipient.Role,
            notificationType: request.Type,
            relatedOrderId: request.RelatedOrderId,
            subject: request.Content.Subject,
            body: request.Content.Body,
            utcNow: startedAtUtc);

        if (createResult.IsFailure)
        {
            LogCreateFailed(logger, createResult.Error.Code, createResult.Error.Description);
            return Result.Failure(createResult.Error);
        }

        Notification notification = createResult.Value;
        notificationRepository.Add(notification);

        // Nivel 2: insert + SaveChanges. Si gana el race contra otro worker, OK. Si pierde, el
        // unique index dispara DbUpdateException -> DuplicateNotificationException (traducido por
        // el SaveChangesAsync de NotificationsDbContext) y aqui se trata como no-op.
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateNotificationException)
        {
            Guid orderId = request.RelatedOrderId;
            Guid recipientId = request.Recipient.UserId;
            string typeName = request.Type.ToString();
            LogRaceLost(logger, orderId, recipientId, typeName);
            return Result.Success();
        }

        // Nivel 3: llamada al canal. Si falla, marca Failed pero NO relanza — el broker no debe
        // reintentar un envio fallido (eso es responsabilidad del job de reintento, follow-up).
        Result<NotificationSendResult> sendResult = await channel.SendAsync(
            new NotificationMessage(
                ToEmail: request.Recipient.Email,
                ToName: request.Recipient.Name,
                Subject: request.Content.Subject,
                Body: request.Content.Body),
            cancellationToken).ConfigureAwait(false);

        DateTime finishedAtUtc = clock.UtcNow;
        if (sendResult.IsSuccess)
        {
            Result markResult = notification.MarkSent(sendResult.Value.ProviderMessageId, finishedAtUtc);
            if (markResult.IsFailure)
            {
                // Solo posible si el estado ya era Failed (no deberia pasar — la fila acaba de insertarse).
                LogMarkFailed(logger, notification.Id, markResult.Error.Code);
            }
        }
        else
        {
            string errorCode = sendResult.Error.Code;
            string errorDesc = sendResult.Error.Description;
            Result markResult = notification.MarkFailed(errorDesc, finishedAtUtc);
            if (markResult.IsFailure)
            {
                LogMarkFailed(logger, notification.Id, markResult.Error.Code);
            }

            Guid orderId = request.RelatedOrderId;
            LogChannelFailed(logger, orderId, errorCode, errorDesc);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Notificacion ya enviada anteriormente (idempotente): OrderId={OrderId} RecipientUserId={RecipientUserId} Type={Type}.")]
    private static partial void LogAlreadyNotified(ILogger logger, Guid orderId, Guid recipientUserId, string type);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No se pudo crear la notificacion: {Code} {Description}.")]
    private static partial void LogCreateFailed(ILogger logger, string code, string description);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Race perdido en insert (otro worker ya inserto): OrderId={OrderId} RecipientUserId={RecipientUserId} Type={Type}. No-op idempotente.")]
    private static partial void LogRaceLost(ILogger logger, Guid orderId, Guid recipientUserId, string type);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No se pudo transitar la notificacion {NotificationId}: {Code}.")]
    private static partial void LogMarkFailed(ILogger logger, Guid notificationId, string code);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "El canal fallo enviando la notificacion del pedido {OrderId}: {Code} {Description}. Quedo registrada como Failed.")]
    private static partial void LogChannelFailed(ILogger logger, Guid orderId, string code, string description);
}
