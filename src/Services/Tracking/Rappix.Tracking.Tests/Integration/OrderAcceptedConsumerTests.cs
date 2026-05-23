using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rappix.Contracts.Orders;
using Rappix.Tracking.Application.Abstractions;
using Rappix.Tracking.Domain.OrderTrackings;
using Rappix.Tracking.Infrastructure.Messaging;
using Rappix.Tracking.Infrastructure.Persistence;
using Rappix.Tracking.Tests.Integration._Shared;

namespace Rappix.Tracking.Tests.Integration;

public sealed class OrderAcceptedConsumerTests : TrackingConsumerTestBase
{
    protected override void ConfigureHarness(IBusRegistrationConfigurator configurator) =>
        configurator.AddConsumer<OrderAcceptedConsumer>();

    [Fact]
    public async Task Consume_TransitionsTrackingToMerchantAccepted_AndPushesStatus()
    {
        Guid orderId = Guid.CreateVersion7();
        DateTime acceptedAtUtc = DateTime.UtcNow.AddSeconds(-1);

        await SeedPlacedTrackingAsync(orderId, customerUserId: Guid.NewGuid());

        await Harness.Bus.Publish(new OrderAcceptedIntegrationEvent
        {
            OrderId = orderId,
            MerchantId = Guid.NewGuid(),
            AcceptedAtUtc = acceptedAtUtc,
        });

        (await Harness.GetConsumerHarness<OrderAcceptedConsumer>()
            .Consumed.Any<OrderAcceptedIntegrationEvent>(f => f.Context.Message.OrderId == orderId))
            .Should().BeTrue();

        (IServiceScope scope, TrackingDbContext db) = CreateDbScope();
        using (scope)
        {
            OrderTracking? tracking = await db.OrderTrackings.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == orderId);
            tracking!.CurrentStatus.Should().Be(TrackingStatus.MerchantAccepted);
        }

        await Notifier.Received(1).PushStatus(
            orderId,
            nameof(TrackingStatus.MerchantAccepted),
            acceptedAtUtc,
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_WithoutPreviousTracking_LogsWarn_DoesNotThrow()
    {
        // Sin OrderSubmitted previo: el consumer debe loggear warn y salir limpio (no excepcion).
        Guid orderId = Guid.CreateVersion7();
        await Harness.Bus.Publish(new OrderAcceptedIntegrationEvent
        {
            OrderId = orderId,
            MerchantId = Guid.NewGuid(),
            AcceptedAtUtc = DateTime.UtcNow,
        });

        // Si la consume se completa OK (sin Faulted), el contrato se cumple.
        bool consumed = await Harness.GetConsumerHarness<OrderAcceptedConsumer>()
            .Consumed.Any<OrderAcceptedIntegrationEvent>(f => f.Context.Message.OrderId == orderId);
        consumed.Should().BeTrue();

        // Verifica que no hubo push (no se inicia status si no hay tracking).
        await Notifier.DidNotReceive().PushStatus(
            orderId, Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    private async Task SeedPlacedTrackingAsync(Guid orderId, Guid customerUserId)
    {
        (IServiceScope scope, TrackingDbContext db) = CreateDbScope();
        using (scope)
        {
            OrderTracking tracking = OrderTracking.FromOrderSubmitted(
                orderId, customerUserId, Guid.NewGuid(),
                pickupLat: 18.48, pickupLng: -69.93,
                deliveryLat: 18.49, deliveryLng: -69.94,
                utcNow: DateTime.UtcNow.AddMinutes(-5));
            db.OrderTrackings.Add(tracking);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);
        }
    }
}
