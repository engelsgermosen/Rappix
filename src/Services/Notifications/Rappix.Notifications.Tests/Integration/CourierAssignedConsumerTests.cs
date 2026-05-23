using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rappix.Contracts.Dispatch;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Domain.NotificationOrders;
using Rappix.Notifications.Domain.Notifications;
using Rappix.Notifications.Domain.UserContacts;
using Rappix.Notifications.Infrastructure.Messaging;
using Rappix.Notifications.Tests.Integration._Shared;

namespace Rappix.Notifications.Tests.Integration;

/// <summary>
/// Pruebas integration del <see cref="CourierAssignedConsumer"/>: setea CourierUserId en
/// NotificationOrder, notifica al CLIENTE (con el nombre del courier) y al COURIER (asignacion).
/// </summary>
public sealed class CourierAssignedConsumerTests : NotificationsConsumerTestBase
{
    protected override void ConfigureHarness(IBusRegistrationConfigurator configurator) =>
        configurator.AddConsumer<CourierAssignedConsumer>();

    [Fact]
    public async Task Consume_NotifiesBothCustomerAndCourier_AndSetsCourierUserIdOnOrder()
    {
        Guid orderId = Guid.CreateVersion7();
        Guid customerUserId = Guid.CreateVersion7();
        Guid merchantId = Guid.CreateVersion7();
        Guid courierUserId = Guid.CreateVersion7();

        await PrePopulateAsync(orderId, customerUserId, merchantId, courierUserId);

        var message = new CourierAssignedIntegrationEvent
        {
            OrderId = orderId,
            CourierId = courierUserId,
        };

        await Harness.Bus.Publish(message);
        (await Harness.GetConsumerHarness<CourierAssignedConsumer>()
            .Consumed.Any<CourierAssignedIntegrationEvent>(filter => filter.Context.Message.OrderId == orderId))
            .Should().BeTrue();

        (IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            // CourierUserId set en la proyeccion (los terminales posteriores lo van a usar).
            NotificationOrder? proj = await db.NotificationOrders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId);
            proj.Should().NotBeNull();
            proj!.CourierUserId.Should().Be(courierUserId);

            // 2 notifications persistidas: una para customer (CourierAssignedForCustomer), otra
            // para courier (CourierAssignedForCourier). El unique index NO las dedupe porque tienen
            // distinto NotificationType — solo dedupe entre tipos iguales.
            var notifications = await db.Notifications.AsNoTracking()
                .Where(n => n.RelatedOrderId == orderId)
                .OrderBy(n => n.NotificationType)
                .ToListAsync();
            notifications.Should().HaveCount(2);

            Notification customerNotif = notifications[0];
            customerNotif.NotificationType.Should().Be(NotificationType.CourierAssignedForCustomer);
            customerNotif.RecipientUserId.Should().Be(customerUserId);
            customerNotif.Status.Should().Be(NotificationStatus.Sent);

            Notification courierNotif = notifications[1];
            courierNotif.NotificationType.Should().Be(NotificationType.CourierAssignedForCourier);
            courierNotif.RecipientUserId.Should().Be(courierUserId);
            courierNotif.Status.Should().Be(NotificationStatus.Sent);
        }

        // Canal recibio 2 llamadas (una por destinatario).
        await Channel.Received(2).SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    private async Task PrePopulateAsync(Guid orderId, Guid customerUserId, Guid merchantId, Guid courierUserId)
    {
        (IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            db.NotificationOrders.Add(NotificationOrder.Create(orderId, customerUserId, merchantId, DateTime.UtcNow));
            db.UserContacts.Add(UserContact.Create(
                userId: customerUserId,
                email: "cliente@test.local",
                firstName: "Juan",
                lastName: "Perez",
                userType: UserType.Customer,
                emailConfirmed: true,
                utcNow: DateTime.UtcNow));
            db.UserContacts.Add(UserContact.Create(
                userId: courierUserId,
                email: "courier@test.local",
                firstName: "Pedro",
                lastName: "Lopez",
                userType: UserType.Courier,
                emailConfirmed: true,
                utcNow: DateTime.UtcNow));
            await db.SaveChangesAsync();
        }
    }
}
