using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.Contracts.Dispatch;
using Rappix.Contracts.Orders;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Application.Notifications;
using Rappix.Notifications.Domain.MerchantContacts;
using Rappix.Notifications.Domain.NotificationOrders;
using Rappix.Notifications.Domain.Notifications;
using Rappix.Notifications.Domain.UserContacts;

namespace Rappix.Notifications.Infrastructure.Messaging;

/// <summary>
/// Multi-IConsumer sobre los 4 eventos terminales del pedido (<c>OrderDelivered</c> de Dispatch,
/// <c>OrderCompleted</c> de Orders saga, <c>OrderCancelled</c> y <c>OrderFailed</c> de la saga).
/// Notifica al CLIENTE y al MERCHANT con la plantilla correspondiente.
/// </summary>
/// <remarks>
/// CRITICO (ADR-0010 D4): <see cref="OrderDeliveredIntegrationEvent"/> y
/// <see cref="OrderCompletedIntegrationEvent"/> mapean al MISMO <see cref="NotificationType.OrderDelivered"/>.
/// El primero inserta la Notification; el segundo recibe <see cref="DuplicateNotificationException"/>
/// del unique index <c>UX_Notification_BusinessKey</c> y NotifyHandler lo trata como no-op. Sin esto
/// el cliente recibiria DOS emails "tu pedido fue entregado" para el mismo pedido. Test critico en
/// commit 10 (DoubleDeliveryIdempotencyTests escenario B).
/// </remarks>
internal sealed partial class OrderTerminalEventsConsumer(
    INotificationOrderRepository orderRepository,
    IMerchantContactRepository merchantRepository,
    IUserContactRepository userRepository,
    INotifyHandler notifyHandler,
    ILogger<OrderTerminalEventsConsumer> logger)
    : IConsumer<OrderDeliveredIntegrationEvent>,
      IConsumer<OrderCompletedIntegrationEvent>,
      IConsumer<OrderCancelledIntegrationEvent>,
      IConsumer<OrderFailedIntegrationEvent>
{
    public Task Consume(ConsumeContext<OrderDeliveredIntegrationEvent> context) =>
        NotifyTerminalAsync(
            context.Message.OrderId,
            sourceMessageId: context.MessageId?.ToString(),
            notificationType: NotificationType.OrderDelivered,
            reason: null,
            context.CancellationToken);

    public Task Consume(ConsumeContext<OrderCompletedIntegrationEvent> context) =>
        // Mismo NotificationType.OrderDelivered que OrderDelivered de Dispatch — el unique index
        // dedupe ambos por (RelatedOrderId, RecipientUserId, NotificationType).
        NotifyTerminalAsync(
            context.Message.OrderId,
            sourceMessageId: context.MessageId?.ToString(),
            notificationType: NotificationType.OrderDelivered,
            reason: null,
            context.CancellationToken);

    public Task Consume(ConsumeContext<OrderCancelledIntegrationEvent> context) =>
        NotifyTerminalAsync(
            context.Message.OrderId,
            sourceMessageId: context.MessageId?.ToString(),
            notificationType: NotificationType.OrderCancelled,
            reason: context.Message.Reason,
            context.CancellationToken);

    public Task Consume(ConsumeContext<OrderFailedIntegrationEvent> context) =>
        NotifyTerminalAsync(
            context.Message.OrderId,
            sourceMessageId: context.MessageId?.ToString(),
            notificationType: NotificationType.OrderFailed,
            reason: context.Message.Reason,
            context.CancellationToken);

    private async Task NotifyTerminalAsync(
        Guid orderId,
        string? sourceMessageId,
        NotificationType notificationType,
        string? reason,
        CancellationToken cancellationToken)
    {
        NotificationOrder? order = await orderRepository.GetByIdAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            LogOrderProjectionMissing(logger, orderId, notificationType.ToString());
            return;
        }

        UserContact? customer = await userRepository.GetByIdAsync(order.CustomerUserId, cancellationToken).ConfigureAwait(false);
        MerchantContact? merchantContact = await merchantRepository.GetByIdAsync(order.MerchantId, cancellationToken).ConfigureAwait(false);
        UserContact? merchantOwner = merchantContact is null
            ? null
            : await userRepository.GetByIdAsync(merchantContact.OwnerUserId, cancellationToken).ConfigureAwait(false);

        // Notificacion al CLIENTE.
        if (customer is not null)
        {
            NotificationContent customerContent = BuildCustomerContent(notificationType, orderId, customer.FirstName, reason);
            await notifyHandler.SendAsync(new NotifyRequest(
                SourceMessageId: sourceMessageId,
                Recipient: new Recipient(customer.Id, customer.Email, customer.FullName, RecipientRole.Customer),
                Type: notificationType,
                RelatedOrderId: orderId,
                Content: customerContent),
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            LogCustomerContactMissing(logger, order.CustomerUserId, orderId);
        }

        // Notificacion al MERCHANT.
        if (merchantOwner is not null)
        {
            NotificationContent merchantContent = BuildMerchantContent(notificationType, orderId, reason);
            await notifyHandler.SendAsync(new NotifyRequest(
                SourceMessageId: sourceMessageId,
                Recipient: new Recipient(merchantOwner.Id, merchantOwner.Email, merchantOwner.FullName, RecipientRole.Merchant),
                Type: notificationType,
                RelatedOrderId: orderId,
                Content: merchantContent),
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            LogMerchantContactMissing(logger, order.MerchantId, orderId);
        }
    }

    private static NotificationContent BuildCustomerContent(NotificationType type, Guid orderId, string customerFirstName, string? reason)
    {
        string safeReason = reason ?? "Sin razon especificada.";
        return type switch
        {
            NotificationType.OrderDelivered => NotificationTemplates.OrderDeliveredForCustomer(orderId, customerFirstName),
            NotificationType.OrderCancelled => NotificationTemplates.OrderCancelledForCustomer(orderId, customerFirstName, safeReason),
            NotificationType.OrderFailed => NotificationTemplates.OrderFailedForCustomer(orderId, customerFirstName, safeReason),
            _ => throw new InvalidOperationException($"Tipo terminal no soportado para customer: {type}."),
        };
    }

    private static NotificationContent BuildMerchantContent(NotificationType type, Guid orderId, string? reason)
    {
        string safeReason = reason ?? "Sin razon especificada.";
        return type switch
        {
            NotificationType.OrderDelivered => NotificationTemplates.OrderDeliveredForMerchant(orderId),
            NotificationType.OrderCancelled => NotificationTemplates.OrderCancelledForMerchant(orderId, safeReason),
            NotificationType.OrderFailed => NotificationTemplates.OrderFailedForMerchant(orderId, safeReason),
            _ => throw new InvalidOperationException($"Tipo terminal no soportado para merchant: {type}."),
        };
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Evento terminal {NotificationType} sin NotificationOrder previo orderId={OrderId}; skip envios (cold-start).")]
    private static partial void LogOrderProjectionMissing(ILogger logger, Guid orderId, string notificationType);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "UserContact missing for customerUserId={CustomerUserId} (orderId={OrderId}); skip envio terminal al cliente (cold-start).")]
    private static partial void LogCustomerContactMissing(ILogger logger, Guid customerUserId, Guid orderId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "MerchantContact o UserContact del owner missing for merchantId={MerchantId} (orderId={OrderId}); skip envio terminal al merchant (cold-start).")]
    private static partial void LogMerchantContactMissing(ILogger logger, Guid merchantId, Guid orderId);
}
