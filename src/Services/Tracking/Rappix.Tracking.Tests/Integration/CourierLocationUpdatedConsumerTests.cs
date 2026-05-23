using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rappix.Contracts.Dispatch;
using Rappix.Tracking.Application.Abstractions;
using Rappix.Tracking.Domain.CourierActiveOrders;
using Rappix.Tracking.Domain.OrderTrackings;
using Rappix.Tracking.Infrastructure.Messaging;
using Rappix.Tracking.Infrastructure.Persistence;
using Rappix.Tracking.Tests.Integration._Shared;

namespace Rappix.Tracking.Tests.Integration;

public sealed class CourierLocationUpdatedConsumerTests : TrackingConsumerTestBase
{
    protected override void ConfigureHarness(IBusRegistrationConfigurator configurator) =>
        configurator.AddConsumer<CourierLocationUpdatedConsumer>();

    [Fact]
    public async Task Consume_WithActiveMapping_UpdatesTracking_AndPushesLocation()
    {
        Guid orderId = Guid.CreateVersion7();
        Guid courierId = Guid.CreateVersion7();
        DateTime reportedAtUtc = DateTime.UtcNow;

        await SeedAssignedAsync(orderId, courierId);

        await Harness.Bus.Publish(new CourierLocationUpdatedIntegrationEvent
        {
            CourierId = courierId,
            Latitude = 18.4861,
            Longitude = -69.9312,
            ReportedAtUtc = reportedAtUtc,
        });

        await Harness.GetConsumerHarness<CourierLocationUpdatedConsumer>()
            .Consumed.Any<CourierLocationUpdatedIntegrationEvent>(f => f.Context.Message.CourierId == courierId);

        (IServiceScope scope, TrackingDbContext db) = CreateDbScope();
        using (scope)
        {
            OrderTracking? tracking = await db.OrderTrackings.AsNoTracking().FirstOrDefaultAsync(t => t.Id == orderId);
            tracking!.LastCourierLat.Should().Be(18.4861);
            tracking.LastCourierLng.Should().Be(-69.9312);
            // PG persiste con precision de microsegundos; .NET DateTime es 100ns. Tolerancia 1ms.
            tracking.LastLocationAtUtc.Should().BeCloseTo(reportedAtUtc, TimeSpan.FromMilliseconds(1));
        }

        await Notifier.Received(1).PushLocation(
            orderId, 18.4861, -69.9312, reportedAtUtc, courierId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_WithoutActiveMapping_SilentlySkipped_NoTrackingTouched_NoPush()
    {
        // Courier reportando ubicacion en idle (no esta asignado): salida silenciosa.
        Guid courierId = Guid.CreateVersion7();

        await Harness.Bus.Publish(new CourierLocationUpdatedIntegrationEvent
        {
            CourierId = courierId,
            Latitude = 18.5,
            Longitude = -69.9,
            ReportedAtUtc = DateTime.UtcNow,
        });

        await Harness.GetConsumerHarness<CourierLocationUpdatedConsumer>()
            .Consumed.Any<CourierLocationUpdatedIntegrationEvent>(f => f.Context.Message.CourierId == courierId);

        await Notifier.DidNotReceive().PushLocation(
            Arg.Any<Guid>(), Arg.Any<double>(), Arg.Any<double>(),
            Arg.Any<DateTime>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_StaleByTimestamp_DiscardedNoSaveNoPush()
    {
        // Segundo evento con timestamp ANTERIOR al primero — debe ser descartado.
        Guid orderId = Guid.CreateVersion7();
        Guid courierId = Guid.CreateVersion7();
        DateTime fresh = DateTime.UtcNow;
        DateTime stale = fresh.AddSeconds(-30); // pre-fresh

        await SeedAssignedAsync(orderId, courierId);

        // Primer publish: fresh.
        await Harness.Bus.Publish(new CourierLocationUpdatedIntegrationEvent
        {
            CourierId = courierId, Latitude = 18.5, Longitude = -69.9, ReportedAtUtc = fresh,
        });
        await Harness.GetConsumerHarness<CourierLocationUpdatedConsumer>()
            .Consumed.Any<CourierLocationUpdatedIntegrationEvent>(f =>
                f.Context.Message.CourierId == courierId && f.Context.Message.ReportedAtUtc == fresh);

        Notifier.ClearReceivedCalls();

        // Segundo publish: stale (timestamp anterior).
        await Harness.Bus.Publish(new CourierLocationUpdatedIntegrationEvent
        {
            CourierId = courierId, Latitude = 99.9, Longitude = -99.9, ReportedAtUtc = stale,
        });
        await Harness.GetConsumerHarness<CourierLocationUpdatedConsumer>()
            .Consumed.Any<CourierLocationUpdatedIntegrationEvent>(f =>
                f.Context.Message.CourierId == courierId && f.Context.Message.ReportedAtUtc == stale);

        // Sin push del segundo: descartado por stale.
        await Notifier.DidNotReceive().PushLocation(
            Arg.Any<Guid>(), Arg.Any<double>(), Arg.Any<double>(),
            Arg.Any<DateTime>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());

        // El tracking sigue con los valores frescos (no se sobreescribio con 99.9).
        (IServiceScope scope, TrackingDbContext db) = CreateDbScope();
        using (scope)
        {
            OrderTracking? tracking = await db.OrderTrackings.AsNoTracking().FirstOrDefaultAsync(t => t.Id == orderId);
            tracking!.LastCourierLat.Should().Be(18.5);
            tracking.LastCourierLng.Should().Be(-69.9);
            tracking.LastLocationAtUtc.Should().BeCloseTo(fresh, TimeSpan.FromMilliseconds(1));
        }
    }

    private async Task SeedAssignedAsync(Guid orderId, Guid courierId)
    {
        (IServiceScope scope, TrackingDbContext db) = CreateDbScope();
        using (scope)
        {
            OrderTracking tracking = OrderTracking.FromOrderSubmitted(
                orderId, Guid.NewGuid(), Guid.NewGuid(),
                18.48, -69.93, 18.49, -69.94,
                DateTime.UtcNow.AddMinutes(-5));
            tracking.ApplyMerchantAccepted(DateTime.UtcNow.AddMinutes(-3));
            tracking.ApplyCourierAssigned(courierId, DateTime.UtcNow.AddMinutes(-1));
            db.OrderTrackings.Add(tracking);
            db.CourierActiveOrders.Add(CourierActiveOrder.Create(courierId, orderId, DateTime.UtcNow.AddMinutes(-1)));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);
        }
    }
}
