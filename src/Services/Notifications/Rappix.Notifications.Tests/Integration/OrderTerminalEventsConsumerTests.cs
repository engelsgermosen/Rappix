using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rappix.Contracts.Dispatch;
using Rappix.Contracts.Orders;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Domain.MerchantContacts;
using Rappix.Notifications.Domain.NotificationOrders;
using Rappix.Notifications.Domain.Notifications;
using Rappix.Notifications.Domain.UserContacts;
using Rappix.Notifications.Infrastructure.Messaging;
using Rappix.Notifications.Tests.Integration._Shared;

namespace Rappix.Notifications.Tests.Integration;

/// <summary>
/// Pruebas integration del <see cref="OrderTerminalEventsConsumer"/>: cada uno de los 4 eventos
/// terminales (Delivered, Completed, Cancelled, Failed) notifica al CLIENTE y al MERCHANT con
/// la plantilla correspondiente. <c>OrderDeliveredIntegrationEvent</c> y <c>OrderCompletedIntegrationEvent</c>
/// mapean al MISMO <see cref="NotificationType.OrderDelivered"/>; este test class verifica el
/// flujo feliz de cada uno. El escenario "Delivered + Completed del mismo pedido = 2 notifs
/// (cust + merch) NO 4" es el corazon del test critico de commit 10 (DoubleDeliveryIdempotencyTests).
/// </summary>
public sealed class OrderTerminalEventsConsumerTests : NotificationsConsumerTestBase
{
    protected override void ConfigureHarness(IBusRegistrationConfigurator configurator) =>
        configurator.AddConsumer<OrderTerminalEventsConsumer>();

    [Fact]
    public async Task Consume_OrderDelivered_NotifiesCustomerAndMerchant_AsOrderDelivered()
    {
        (Guid orderId, Guid customerUserId, Guid merchantId, Guid ownerUserId) = await SetupOrderAsync();

        await Harness.Bus.Publish(new OrderDeliveredIntegrationEvent
        {
            OrderId = orderId,
            DeliveredAtUtc = DateTime.UtcNow,
        });
        await Harness.GetConsumerHarness<OrderTerminalEventsConsumer>()
            .Consumed.Any<OrderDeliveredIntegrationEvent>(filter => filter.Context.Message.OrderId == orderId);

        await AssertTerminalNotificationsAsync(orderId, customerUserId, ownerUserId, NotificationType.OrderDelivered);
    }

    [Fact]
    public async Task Consume_OrderCompleted_AlsoEmitsOrderDeliveredNotificationType()
    {
        // Aqui solo verificamos que OrderCompleted mapea al MISMO NotificationType.OrderDelivered —
        // el caso interesante (Delivered + Completed seguidos = 2 notifs no 4) vive en commit 10.
        (Guid orderId, Guid customerUserId, Guid merchantId, Guid ownerUserId) = await SetupOrderAsync();

        await Harness.Bus.Publish(new OrderCompletedIntegrationEvent
        {
            OrderId = orderId,
            CustomerUserId = customerUserId,
            MerchantId = merchantId,
            CompletedAtUtc = DateTime.UtcNow,
        });
        await Harness.GetConsumerHarness<OrderTerminalEventsConsumer>()
            .Consumed.Any<OrderCompletedIntegrationEvent>(filter => filter.Context.Message.OrderId == orderId);

        await AssertTerminalNotificationsAsync(orderId, customerUserId, ownerUserId, NotificationType.OrderDelivered);
    }

    [Fact]
    public async Task Consume_OrderCancelled_NotifiesCustomerAndMerchant_WithReason()
    {
        (Guid orderId, Guid customerUserId, Guid merchantId, Guid ownerUserId) = await SetupOrderAsync();

        await Harness.Bus.Publish(new OrderCancelledIntegrationEvent
        {
            OrderId = orderId,
            Reason = "Merchant rechazo",
            CancelledAtUtc = DateTime.UtcNow,
        });
        await Harness.GetConsumerHarness<OrderTerminalEventsConsumer>()
            .Consumed.Any<OrderCancelledIntegrationEvent>(filter => filter.Context.Message.OrderId == orderId);

        await AssertTerminalNotificationsAsync(orderId, customerUserId, ownerUserId, NotificationType.OrderCancelled);

        // El reason debe aparecer en el body de ambos emails.
        await Channel.Received(1).SendAsync(
            Arg.Is<NotificationMessage>(m => m.Body.Contains("Merchant rechazo") && m.ToEmail == "cliente@test.local"),
            Arg.Any<CancellationToken>());
        await Channel.Received(1).SendAsync(
            Arg.Is<NotificationMessage>(m => m.Body.Contains("Merchant rechazo") && m.ToEmail == "owner@merchant.local"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_OrderFailed_NotifiesCustomerAndMerchant()
    {
        (Guid orderId, Guid customerUserId, Guid merchantId, Guid ownerUserId) = await SetupOrderAsync();

        await Harness.Bus.Publish(new OrderFailedIntegrationEvent
        {
            OrderId = orderId,
            Reason = "Stock insuficiente",
            FailedAtUtc = DateTime.UtcNow,
        });
        await Harness.GetConsumerHarness<OrderTerminalEventsConsumer>()
            .Consumed.Any<OrderFailedIntegrationEvent>(filter => filter.Context.Message.OrderId == orderId);

        await AssertTerminalNotificationsAsync(orderId, customerUserId, ownerUserId, NotificationType.OrderFailed);
    }

    /// <summary>Helper: assert que se persisten 2 Notifications (customer + merchant) con el tipo esperado.</summary>
    private async Task AssertTerminalNotificationsAsync(Guid orderId, Guid customerUserId, Guid ownerUserId, NotificationType expectedType)
    {
        (IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            var notifications = await db.Notifications.AsNoTracking()
                .Where(n => n.RelatedOrderId == orderId)
                .ToListAsync();

            notifications.Should().HaveCount(2);
            notifications.Should().AllSatisfy(n =>
            {
                n.NotificationType.Should().Be(expectedType);
                n.Status.Should().Be(NotificationStatus.Sent);
            });
            notifications.Should().Contain(n => n.RecipientUserId == customerUserId && n.RecipientRole == RecipientRole.Customer);
            notifications.Should().Contain(n => n.RecipientUserId == ownerUserId && n.RecipientRole == RecipientRole.Merchant);
        }
    }

    /// <summary>Pre-poblar las 4 proyecciones necesarias (NotificationOrder + MerchantContact + 2 UserContacts).</summary>
    private async Task<(Guid OrderId, Guid CustomerUserId, Guid MerchantId, Guid OwnerUserId)> SetupOrderAsync()
    {
        Guid orderId = Guid.CreateVersion7();
        Guid customerUserId = Guid.CreateVersion7();
        Guid merchantId = Guid.CreateVersion7();
        Guid ownerUserId = Guid.CreateVersion7();

        (IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            db.NotificationOrders.Add(NotificationOrder.Create(orderId, customerUserId, merchantId, DateTime.UtcNow));
            db.MerchantContacts.Add(MerchantContact.Create(merchantId, ownerUserId, DateTime.UtcNow));
            db.UserContacts.Add(UserContact.Create(
                userId: customerUserId,
                email: "cliente@test.local",
                firstName: "Juan",
                lastName: "Perez",
                userType: UserType.Customer,
                emailConfirmed: true,
                utcNow: DateTime.UtcNow));
            db.UserContacts.Add(UserContact.Create(
                userId: ownerUserId,
                email: "owner@merchant.local",
                firstName: "Maria",
                lastName: "Garcia",
                userType: UserType.Merchant,
                emailConfirmed: true,
                utcNow: DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        return (orderId, customerUserId, merchantId, ownerUserId);
    }
}
