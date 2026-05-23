using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Orders;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Application.Notifications;
using Rappix.Notifications.Domain.MerchantContacts;
using Rappix.Notifications.Domain.NotificationOrders;
using Rappix.Notifications.Domain.Notifications;
using Rappix.Notifications.Domain.UserContacts;

namespace Rappix.Notifications.Infrastructure.Messaging;

/// <summary>
/// Consume <see cref="OrderSubmittedIntegrationEvent"/> y hace DOS cosas: (1) proyecta el pedido en
/// <see cref="NotificationOrder"/> (que los consumers posteriores leen para resolver
/// CustomerUserId + MerchantId que sus eventos NO llevan); (2) notifica al MERCHANT con la
/// plantilla <c>NewOrderForMerchant</c>. La proyeccion + la notificacion ocurren en el MISMO
/// SaveChanges del outbox callback — atomico.
/// </summary>
/// <remarks>
/// Resolucion del merchant a email (dos hops): MerchantId -&gt; MerchantContact.OwnerUserId -&gt;
/// UserContact.Email. Si alguna proyeccion falta (cold-start gap), logea Warning y SALTA el envio
/// del email — la proyeccion NotificationOrder ya se persistio. Una iteracion futura podria sumar
/// un job de reconciliacion que detecte (NotificationOrder existe + sin Notification correspondiente)
/// para reintentar emails perdidos.
/// </remarks>
internal sealed partial class OrderSubmittedConsumer(
    INotificationOrderRepository orderRepository,
    IMerchantContactRepository merchantRepository,
    IUserContactRepository userRepository,
    INotifyHandler notifyHandler,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<OrderSubmittedConsumer> logger) : IConsumer<OrderSubmittedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<OrderSubmittedIntegrationEvent> context)
    {
        OrderSubmittedIntegrationEvent message = context.Message;
        CancellationToken ct = context.CancellationToken;

        // 1. Proyecta NotificationOrder (idempotente — si ya existe, skip insert; el inbox EF deduplica
        // redeliveries del MISMO MessageId, pero defensa contra escenarios edge).
        NotificationOrder? existing = await orderRepository.GetByIdAsync(message.OrderId, ct).ConfigureAwait(false);
        if (existing is null)
        {
            NotificationOrder order = NotificationOrder.Create(
                orderId: message.OrderId,
                customerUserId: message.CustomerUserId,
                merchantId: message.MerchantId,
                utcNow: clock.UtcNow);
            orderRepository.Add(order);
            await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
            LogProjected(logger, message.OrderId, message.CustomerUserId, message.MerchantId);
        }

        // 2. Notifica al merchant. Si la cadena de resolucion falla (MerchantContact o UserContact
        // ausentes — cold-start), logea Warning y SALE: la proyeccion ya quedo persistida, el envio
        // se reintenta en un job de reconciliacion futuro.
        MerchantContact? merchant = await merchantRepository.GetByIdAsync(message.MerchantId, ct).ConfigureAwait(false);
        if (merchant is null)
        {
            LogMerchantContactMissing(logger, message.MerchantId, message.OrderId);
            return;
        }

        UserContact? owner = await userRepository.GetByIdAsync(merchant.OwnerUserId, ct).ConfigureAwait(false);
        if (owner is null)
        {
            LogUserContactMissing(logger, merchant.OwnerUserId, message.OrderId);
            return;
        }

        NotificationContent content = NotificationTemplates.NewOrderForMerchant(
            orderId: message.OrderId,
            total: message.TotalAmount,
            currency: message.Currency,
            deliveryAddress: message.DeliveryAddress);

        var request = new NotifyRequest(
            SourceMessageId: context.MessageId?.ToString(),
            Recipient: new Recipient(
                UserId: owner.Id,
                Email: owner.Email,
                Name: owner.FullName,
                Role: RecipientRole.Merchant),
            Type: NotificationType.NewOrder,
            RelatedOrderId: message.OrderId,
            Content: content);

        await notifyHandler.SendAsync(request, ct).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "NotificationOrder proyectado orderId={OrderId} customerUserId={CustomerUserId} merchantId={MerchantId}.")]
    private static partial void LogProjected(ILogger logger, Guid orderId, Guid customerUserId, Guid merchantId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "MerchantContact missing for merchantId={MerchantId} (orderId={OrderId}); skip envio al merchant (cold-start; reconciliacion via job futuro).")]
    private static partial void LogMerchantContactMissing(ILogger logger, Guid merchantId, Guid orderId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "UserContact missing for ownerUserId={OwnerUserId} (orderId={OrderId}); skip envio al merchant (cold-start).")]
    private static partial void LogUserContactMissing(ILogger logger, Guid ownerUserId, Guid orderId);
}
