using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Dispatch;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Application.Notifications;
using Rappix.Notifications.Domain.NotificationOrders;
using Rappix.Notifications.Domain.Notifications;
using Rappix.Notifications.Domain.UserContacts;

namespace Rappix.Notifications.Infrastructure.Messaging;

/// <summary>
/// Consume <see cref="CourierAssignedIntegrationEvent"/> y hace tres cosas: (1) setea
/// <see cref="NotificationOrder.CourierUserId"/> en la proyeccion para que los consumers terminales
/// posteriores puedan notificarle al courier; (2) notifica al CLIENTE ("tu courier esta en camino");
/// (3) notifica al COURIER ("nueva asignacion").
/// </summary>
/// <remarks>
/// El <c>CourierId</c> del evento ES el <c>UserId</c> de Identity por diseno 1-1 de Dispatch (memo
/// dispatch-service.md: CourierProfile.Id == UserId). Por eso aqui se usa directamente como key
/// para resolver <see cref="UserContact"/> sin lookup intermedio (a diferencia del merchant que
/// requiere dos hops via MerchantContact).
/// </remarks>
internal sealed partial class CourierAssignedConsumer(
    INotificationOrderRepository orderRepository,
    IUserContactRepository userRepository,
    INotifyHandler notifyHandler,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<CourierAssignedConsumer> logger) : IConsumer<CourierAssignedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<CourierAssignedIntegrationEvent> context)
    {
        CourierAssignedIntegrationEvent message = context.Message;
        CancellationToken ct = context.CancellationToken;
        Guid courierUserId = message.CourierId; // CourierId == UserId por diseno Dispatch (1-1).

        NotificationOrder? order = await orderRepository.GetByIdAsync(message.OrderId, ct).ConfigureAwait(false);
        if (order is null)
        {
            LogOrderProjectionMissing(logger, message.OrderId);
            return;
        }

        // 1. Setea CourierUserId en la proyeccion (idempotente con el mismo courier).
        order.SetCourier(courierUserId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        // Resuelve los dos destinatarios.
        UserContact? customer = await userRepository.GetByIdAsync(order.CustomerUserId, ct).ConfigureAwait(false);
        UserContact? courier = await userRepository.GetByIdAsync(courierUserId, ct).ConfigureAwait(false);

        // 2. Notifica al CLIENTE (si tenemos su contacto Y el del courier — el nombre del courier va
        // en el subject/body del email al cliente).
        if (customer is not null && courier is not null)
        {
            await notifyHandler.SendAsync(new NotifyRequest(
                SourceMessageId: context.MessageId?.ToString(),
                Recipient: new Recipient(customer.Id, customer.Email, customer.FullName, RecipientRole.Customer),
                Type: NotificationType.CourierAssignedForCustomer,
                RelatedOrderId: message.OrderId,
                Content: NotificationTemplates.CourierAssignedForCustomer(message.OrderId, courier.FirstName)),
                ct).ConfigureAwait(false);
        }
        else
        {
            if (customer is null)
            {
                LogCustomerContactMissing(logger, order.CustomerUserId, message.OrderId);
            }
            // Si el courier es null aqui pero el customer existe, ya no se notifica al cliente porque
            // el nombre del courier es parte de la plantilla — sin nombre no enviamos un email roto.
        }

        // 3. Notifica al COURIER (independiente del envio al cliente).
        if (courier is not null)
        {
            await notifyHandler.SendAsync(new NotifyRequest(
                SourceMessageId: context.MessageId?.ToString(),
                Recipient: new Recipient(courier.Id, courier.Email, courier.FullName, RecipientRole.Courier),
                Type: NotificationType.CourierAssignedForCourier,
                RelatedOrderId: message.OrderId,
                Content: NotificationTemplates.CourierAssignedForCourier(message.OrderId, courier.FirstName)),
                ct).ConfigureAwait(false);
        }
        else
        {
            LogCourierContactMissing(logger, courierUserId, message.OrderId);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "CourierAssigned sin NotificationOrder previo orderId={OrderId}; skip envios (cold-start).")]
    private static partial void LogOrderProjectionMissing(ILogger logger, Guid orderId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "UserContact missing for customerUserId={CustomerUserId} (orderId={OrderId}); skip envio al cliente (cold-start).")]
    private static partial void LogCustomerContactMissing(ILogger logger, Guid customerUserId, Guid orderId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "UserContact missing for courierUserId={CourierUserId} (orderId={OrderId}); skip envio al courier (cold-start; CourierProfile sin sync con UserRegistered?).")]
    private static partial void LogCourierContactMissing(ILogger logger, Guid courierUserId, Guid orderId);
}
