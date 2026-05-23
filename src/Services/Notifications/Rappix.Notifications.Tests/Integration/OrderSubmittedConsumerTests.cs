using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rappix.BuildingBlocks.Core.Results;
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
/// Pruebas integration del <see cref="OrderSubmittedConsumer"/>: verifica que (1) proyecta el
/// pedido en NotificationOrder, (2) resuelve MerchantContact + UserContact en cascada para
/// obtener el email del owner del merchant, (3) llama al INotificationChannel mockeado con los
/// args correctos y (4) persiste la Notification como Sent.
/// </summary>
public sealed class OrderSubmittedConsumerTests : NotificationsConsumerTestBase
{
    protected override void ConfigureHarness(IBusRegistrationConfigurator configurator) =>
        configurator.AddConsumer<OrderSubmittedConsumer>();

    [Fact]
    public async Task Consume_WithMerchantAndOwnerProjections_NotifiesMerchant_AndPersistsSent()
    {
        Guid orderId = Guid.CreateVersion7();
        Guid customerUserId = Guid.CreateVersion7();
        Guid merchantId = Guid.CreateVersion7();
        Guid ownerUserId = Guid.CreateVersion7();

        await PrePopulateContactsAsync(merchantId, ownerUserId, ownerEmail: "owner@merchant.local", ownerFirst: "Maria", ownerLast: "Garcia");

        var message = NewOrderSubmittedFor(orderId, customerUserId, merchantId);

        await Harness.Bus.Publish(message);
        (await Harness.GetConsumerHarness<OrderSubmittedConsumer>()
            .Consumed.Any<OrderSubmittedIntegrationEvent>(filter => filter.Context.Message.OrderId == orderId))
            .Should().BeTrue();

        // (1) NotificationOrder proyectado.
        (IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            NotificationOrder? projected = await db.NotificationOrders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId);
            projected.Should().NotBeNull();
            projected!.CustomerUserId.Should().Be(customerUserId);
            projected.MerchantId.Should().Be(merchantId);
            projected.CourierUserId.Should().BeNull();

            // (4) Notification persistida como Sent al merchant.
            Notification? notification = await db.Notifications.AsNoTracking()
                .FirstOrDefaultAsync(n => n.RelatedOrderId == orderId && n.NotificationType == NotificationType.NewOrder);
            notification.Should().NotBeNull();
            notification!.RecipientUserId.Should().Be(ownerUserId);
            notification.RecipientEmail.Should().Be("owner@merchant.local");
            notification.RecipientRole.Should().Be(RecipientRole.Merchant);
            notification.Status.Should().Be(NotificationStatus.Sent);
        }

        // (3) Canal mockeado llamado con los args correctos.
        await Channel.Received(1).SendAsync(
            Arg.Is<NotificationMessage>(m =>
                m.ToEmail == "owner@merchant.local" &&
                m.ToName == "Maria Garcia" &&
                m.Subject.StartsWith("Nuevo pedido entrante") &&
                m.Body.Contains("250.50 DOP")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_WithoutMerchantContact_ProjectsOrder_ButDoesNotNotify()
    {
        Guid orderId = Guid.CreateVersion7();
        Guid merchantId = Guid.CreateVersion7();
        // NO PrePopulateContactsAsync — la proyeccion del merchant esta vacia (cold-start).

        var message = NewOrderSubmittedFor(orderId, Guid.CreateVersion7(), merchantId);

        await Harness.Bus.Publish(message);
        await Harness.GetConsumerHarness<OrderSubmittedConsumer>()
            .Consumed.Any<OrderSubmittedIntegrationEvent>(filter => filter.Context.Message.OrderId == orderId);

        (IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            // NotificationOrder igual se persiste (lo necesitan los consumers posteriores).
            (await db.NotificationOrders.AsNoTracking().AnyAsync(o => o.Id == orderId)).Should().BeTrue();
            // PERO no se persiste Notification (el envio se saltea por cold-start).
            (await db.Notifications.AsNoTracking().AnyAsync(n => n.RelatedOrderId == orderId)).Should().BeFalse();
        }

        // Canal NO llamado — log Warning fue suficiente.
        await Channel.DidNotReceive().SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_WhenChannelFails_PersistsAsFailed()
    {
        Guid orderId = Guid.CreateVersion7();
        Guid merchantId = Guid.CreateVersion7();
        Guid ownerUserId = Guid.CreateVersion7();

        await PrePopulateContactsAsync(merchantId, ownerUserId, ownerEmail: "owner@merchant.local", ownerFirst: "Maria", ownerLast: "Garcia");

        // Configura el canal para devolver Failure (sobreescribe el default Success del base).
        Channel.SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<NotificationSendResult>(
                Error.Failure("Notifications.Channel.SendFailed", "SendGrid devolvio 500.")));

        var message = NewOrderSubmittedFor(orderId, Guid.CreateVersion7(), merchantId);

        await Harness.Bus.Publish(message);
        await Harness.GetConsumerHarness<OrderSubmittedConsumer>()
            .Consumed.Any<OrderSubmittedIntegrationEvent>(filter => filter.Context.Message.OrderId == orderId);

        (IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            Notification? n = await db.Notifications.AsNoTracking()
                .FirstOrDefaultAsync(x => x.RelatedOrderId == orderId);
            n.Should().NotBeNull();
            n!.Status.Should().Be(NotificationStatus.Failed);
            n.ErrorReason.Should().Be("SendGrid devolvio 500.");
            n.SentAtUtc.Should().NotBeNull();
        }
    }

    private async Task PrePopulateContactsAsync(Guid merchantId, Guid ownerUserId, string ownerEmail, string ownerFirst, string ownerLast)
    {
        (IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            db.MerchantContacts.Add(MerchantContact.Create(merchantId, ownerUserId, DateTime.UtcNow));
            db.UserContacts.Add(UserContact.Create(
                userId: ownerUserId,
                email: ownerEmail,
                firstName: ownerFirst,
                lastName: ownerLast,
                userType: UserType.Merchant,
                emailConfirmed: true,
                utcNow: DateTime.UtcNow));
            await db.SaveChangesAsync();
        }
    }

    private static OrderSubmittedIntegrationEvent NewOrderSubmittedFor(Guid orderId, Guid customerUserId, Guid merchantId) => new()
    {
        OrderId = orderId,
        CustomerUserId = customerUserId,
        MerchantId = merchantId,
        QuoteId = Guid.NewGuid(),
        TotalAmount = 250.50m,
        Currency = "DOP",
        DeliveryAddress = "Av. Winston Churchill 1234",
        DeliveryLatitude = 18.4861d,
        DeliveryLongitude = -69.9312d,
        PickupLatitude = 18.4900d,
        PickupLongitude = -69.9400d,
    };
}
