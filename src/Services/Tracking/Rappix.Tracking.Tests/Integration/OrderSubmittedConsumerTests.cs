using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rappix.Contracts.Orders;
using Rappix.Tracking.Domain.OrderTrackings;
using Rappix.Tracking.Infrastructure.Messaging;
using Rappix.Tracking.Infrastructure.Persistence;
using Rappix.Tracking.Tests.Integration._Shared;

namespace Rappix.Tracking.Tests.Integration;

public sealed class OrderSubmittedConsumerTests : TrackingConsumerTestBase
{
    protected override void ConfigureHarness(IBusRegistrationConfigurator configurator) =>
        configurator.AddConsumer<OrderSubmittedConsumer>();

    [Fact]
    public async Task Consume_CreatesOrderTracking_InPlacedStatus_AndPushesStatus()
    {
        OrderSubmittedIntegrationEvent message = SubmittedFor(orderId: Guid.CreateVersion7(), customerUserId: Guid.NewGuid());

        await Harness.Bus.Publish(message);
        (await Harness.GetConsumerHarness<OrderSubmittedConsumer>()
            .Consumed.Any<OrderSubmittedIntegrationEvent>(filter => filter.Context.Message.OrderId == message.OrderId))
            .Should().BeTrue();

        (IServiceScope scope, TrackingDbContext db) = CreateDbScope();
        using (scope)
        {
            OrderTracking? tracking = await db.OrderTrackings.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == message.OrderId);

            tracking.Should().NotBeNull();
            tracking!.CustomerUserId.Should().Be(message.CustomerUserId);
            tracking.MerchantId.Should().Be(message.MerchantId);
            tracking.CurrentStatus.Should().Be(TrackingStatus.Placed);
            tracking.PickupLat.Should().Be(message.PickupLatitude);
            tracking.PickupLng.Should().Be(message.PickupLongitude);
            tracking.DeliveryLat.Should().Be(message.DeliveryLatitude);
            tracking.DeliveryLng.Should().Be(message.DeliveryLongitude);
        }

        await Notifier.Received(1).PushStatus(
            message.OrderId,
            nameof(TrackingStatus.Placed),
            Arg.Any<DateTime>(),
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_IsIdempotent_DoesNotDuplicateRow()
    {
        Guid orderId = Guid.CreateVersion7();
        OrderSubmittedIntegrationEvent message = SubmittedFor(orderId: orderId, customerUserId: Guid.NewGuid());

        // Primer publish.
        await Harness.Bus.Publish(message);
        await Harness.GetConsumerHarness<OrderSubmittedConsumer>()
            .Consumed.Any<OrderSubmittedIntegrationEvent>(filter => filter.Context.Message.OrderId == orderId);

        // Segundo publish del MISMO mensaje (mismo MessageId): el inbox EF debe dedupicar; el filtro
        // del Apply en el consumer (GetByOrderIdAsync != null) es la defensa en profundidad.
        await Harness.Bus.Publish(message);

        // Espera un poco para que el segundo publish llegue.
        await Task.Delay(500);

        (IServiceScope scope, TrackingDbContext db) = CreateDbScope();
        using (scope)
        {
            int count = await db.OrderTrackings.AsNoTracking().CountAsync(t => t.Id == orderId);
            count.Should().Be(1);
        }
    }

    private static OrderSubmittedIntegrationEvent SubmittedFor(Guid orderId, Guid customerUserId) => new()
    {
        OrderId = orderId,
        CustomerUserId = customerUserId,
        MerchantId = Guid.NewGuid(),
        QuoteId = Guid.NewGuid(),
        TotalAmount = 250m,
        Currency = "DOP",
        DeliveryAddress = "Av. Winston Churchill 1234",
        DeliveryReference = null,
        DeliveryLatitude = 18.4861d,
        DeliveryLongitude = -69.9312d,
        PickupLatitude = 18.4900d,
        PickupLongitude = -69.9400d,
        // Fase 13.6.
        MerchantName = "Comercio Test",
        Lines = [new OrderLineSnapshot("Pizza", 2)],
    };
}
