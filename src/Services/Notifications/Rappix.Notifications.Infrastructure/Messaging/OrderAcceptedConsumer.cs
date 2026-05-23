using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.Contracts.Orders;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Application.Notifications;
using Rappix.Notifications.Domain.NotificationOrders;
using Rappix.Notifications.Domain.Notifications;
using Rappix.Notifications.Domain.UserContacts;

namespace Rappix.Notifications.Infrastructure.Messaging;

/// <summary>
/// Consume <see cref="OrderAcceptedIntegrationEvent"/> y notifica al CLIENTE ("tu pedido fue
/// aceptado"). El evento NO lleva CustomerUserId — lo lee de la proyeccion <see cref="NotificationOrder"/>
/// que <c>OrderSubmittedConsumer</c> creo previamente.
/// </summary>
internal sealed partial class OrderAcceptedConsumer(
    INotificationOrderRepository orderRepository,
    IUserContactRepository userRepository,
    INotifyHandler notifyHandler,
    ILogger<OrderAcceptedConsumer> logger) : IConsumer<OrderAcceptedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<OrderAcceptedIntegrationEvent> context)
    {
        OrderAcceptedIntegrationEvent message = context.Message;
        CancellationToken ct = context.CancellationToken;

        NotificationOrder? order = await orderRepository.GetByIdAsync(message.OrderId, ct).ConfigureAwait(false);
        if (order is null)
        {
            // Edge-case: OrderAccepted llego antes que OrderSubmitted se proyectara, o Notifications
            // arranco entre los dos eventos. Log + return; el evento ya fue consumido (no relanzamos,
            // no queremos broker retry loop por dato faltante de cold-start).
            LogOrderProjectionMissing(logger, message.OrderId);
            return;
        }

        UserContact? customer = await userRepository.GetByIdAsync(order.CustomerUserId, ct).ConfigureAwait(false);
        if (customer is null)
        {
            LogCustomerContactMissing(logger, order.CustomerUserId, message.OrderId);
            return;
        }

        NotificationContent content = NotificationTemplates.OrderAcceptedForCustomer(message.OrderId, customer.FirstName);

        var request = new NotifyRequest(
            SourceMessageId: context.MessageId?.ToString(),
            Recipient: new Recipient(
                UserId: customer.Id,
                Email: customer.Email,
                Name: customer.FullName,
                Role: RecipientRole.Customer),
            Type: NotificationType.OrderAccepted,
            RelatedOrderId: message.OrderId,
            Content: content);

        await notifyHandler.SendAsync(request, ct).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "OrderAccepted sin NotificationOrder previo orderId={OrderId}; skip envio (cold-start).")]
    private static partial void LogOrderProjectionMissing(ILogger logger, Guid orderId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "UserContact missing for customerUserId={CustomerUserId} (orderId={OrderId}); skip envio (cold-start).")]
    private static partial void LogCustomerContactMissing(ILogger logger, Guid customerUserId, Guid orderId);
}
