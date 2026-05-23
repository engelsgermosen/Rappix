using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rappix.Contracts.Orders;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Domain.NotificationOrders;
using Rappix.Notifications.Domain.Notifications;
using Rappix.Notifications.Domain.UserContacts;
using Rappix.Notifications.Infrastructure.Messaging;
using Rappix.Notifications.Tests.Integration._Shared;

namespace Rappix.Notifications.Tests.Integration;

/// <summary>
/// Pruebas integration del <see cref="OrderAcceptedConsumer"/>: verifica que lookup la proyeccion
/// NotificationOrder (no llega en el evento), resuelve el UserContact del customer y notifica
/// con la plantilla <c>OrderAcceptedForCustomer</c>.
/// </summary>
public sealed class OrderAcceptedConsumerTests : NotificationsConsumerTestBase
{
    protected override void ConfigureHarness(IBusRegistrationConfigurator configurator) =>
        configurator.AddConsumer<OrderAcceptedConsumer>();

    [Fact]
    public async Task Consume_WithProjectionAndCustomer_NotifiesCustomer_AndPersistsSent()
    {
        Guid orderId = Guid.CreateVersion7();
        Guid customerUserId = Guid.CreateVersion7();
        Guid merchantId = Guid.CreateVersion7();

        // Pre-condicion: existe la proyeccion NotificationOrder (creada por OrderSubmitted previo
        // en el flujo real, aqui la sembramos directamente para aislar el test del OrderSubmittedConsumer).
        await PrePopulateAsync(orderId, customerUserId, merchantId, customerEmail: "cliente@test.local", customerFirst: "Juan", customerLast: "Perez");

        var message = new OrderAcceptedIntegrationEvent
        {
            OrderId = orderId,
            MerchantId = merchantId,
            AcceptedAtUtc = DateTime.UtcNow,
        };

        await Harness.Bus.Publish(message);
        (await Harness.GetConsumerHarness<OrderAcceptedConsumer>()
            .Consumed.Any<OrderAcceptedIntegrationEvent>(filter => filter.Context.Message.OrderId == orderId))
            .Should().BeTrue();

        (IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            Notification? notification = await db.Notifications.AsNoTracking()
                .FirstOrDefaultAsync(n => n.RelatedOrderId == orderId && n.NotificationType == NotificationType.OrderAccepted);
            notification.Should().NotBeNull();
            notification!.RecipientUserId.Should().Be(customerUserId);
            notification.RecipientEmail.Should().Be("cliente@test.local");
            notification.RecipientRole.Should().Be(RecipientRole.Customer);
            notification.Status.Should().Be(NotificationStatus.Sent);
        }

        await Channel.Received(1).SendAsync(
            Arg.Is<NotificationMessage>(m =>
                m.ToEmail == "cliente@test.local" &&
                m.ToName == "Juan Perez" &&
                m.Subject.Contains("aceptado") &&
                m.Body.Contains("Juan")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_WithoutNotificationOrderProjection_DoesNotNotify()
    {
        // No PrePopulate — la proyeccion NotificationOrder no existe.
        var message = new OrderAcceptedIntegrationEvent
        {
            OrderId = Guid.CreateVersion7(),
            MerchantId = Guid.CreateVersion7(),
            AcceptedAtUtc = DateTime.UtcNow,
        };

        await Harness.Bus.Publish(message);
        await Harness.GetConsumerHarness<OrderAcceptedConsumer>()
            .Consumed.Any<OrderAcceptedIntegrationEvent>(filter => filter.Context.Message.OrderId == message.OrderId);

        (IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            (await db.Notifications.AsNoTracking().AnyAsync(n => n.RelatedOrderId == message.OrderId))
                .Should().BeFalse();
        }

        await Channel.DidNotReceive().SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    private async Task PrePopulateAsync(Guid orderId, Guid customerUserId, Guid merchantId, string customerEmail, string customerFirst, string customerLast)
    {
        (IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            db.NotificationOrders.Add(NotificationOrder.Create(orderId, customerUserId, merchantId, DateTime.UtcNow));
            db.UserContacts.Add(UserContact.Create(
                userId: customerUserId,
                email: customerEmail,
                firstName: customerFirst,
                lastName: customerLast,
                userType: UserType.Customer,
                emailConfirmed: true,
                utcNow: DateTime.UtcNow));
            await db.SaveChangesAsync();
        }
    }
}
