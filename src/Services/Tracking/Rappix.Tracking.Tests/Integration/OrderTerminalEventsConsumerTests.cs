using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rappix.Contracts.Dispatch;
using Rappix.Contracts.Orders;
using Rappix.Tracking.Application.Abstractions;
using Rappix.Tracking.Domain.CourierActiveOrders;
using Rappix.Tracking.Domain.OrderTrackings;
using Rappix.Tracking.Infrastructure.Messaging;
using Rappix.Tracking.Infrastructure.Persistence;
using Rappix.Tracking.Tests.Integration._Shared;

namespace Rappix.Tracking.Tests.Integration;

public sealed class OrderTerminalEventsConsumerTests : TrackingConsumerTestBase
{
    protected override void ConfigureHarness(IBusRegistrationConfigurator configurator) =>
        configurator.AddConsumer<OrderTerminalEventsConsumer>();

    [Fact]
    public async Task OrderDelivered_TransitionsToDelivered_DeletesMapping_PushesStatus()
    {
        Guid orderId = Guid.CreateVersion7();
        Guid courierId = Guid.CreateVersion7();
        DateTime deliveredAtUtc = DateTime.UtcNow;

        await SeedAssignedAsync(orderId, courierId);

        await Harness.Bus.Publish(new OrderDeliveredIntegrationEvent
        {
            OrderId = orderId, DeliveredAtUtc = deliveredAtUtc,
        });
        await Harness.GetConsumerHarness<OrderTerminalEventsConsumer>()
            .Consumed.Any<OrderDeliveredIntegrationEvent>(f => f.Context.Message.OrderId == orderId);

        (IServiceScope scope, TrackingDbContext db) = CreateDbScope();
        using (scope)
        {
            OrderTracking? tracking = await db.OrderTrackings.AsNoTracking().FirstOrDefaultAsync(t => t.Id == orderId);
            tracking!.CurrentStatus.Should().Be(TrackingStatus.Delivered);

            int mappings = await db.CourierActiveOrders.AsNoTracking().CountAsync(m => m.OrderId == orderId);
            mappings.Should().Be(0);
        }

        await Notifier.Received(1).PushStatus(
            orderId, nameof(TrackingStatus.Delivered), deliveredAtUtc, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrderCompleted_AfterOrderDelivered_IsIdempotent_NoDoublePush()
    {
        Guid orderId = Guid.CreateVersion7();
        Guid courierId = Guid.CreateVersion7();
        DateTime deliveredAt = DateTime.UtcNow.AddSeconds(-1);
        DateTime completedAt = DateTime.UtcNow;

        await SeedAssignedAsync(orderId, courierId);

        // Primero OrderDelivered -> transiciona y pushea.
        await Harness.Bus.Publish(new OrderDeliveredIntegrationEvent { OrderId = orderId, DeliveredAtUtc = deliveredAt });
        await Harness.GetConsumerHarness<OrderTerminalEventsConsumer>()
            .Consumed.Any<OrderDeliveredIntegrationEvent>(f => f.Context.Message.OrderId == orderId);

        Notifier.ClearReceivedCalls();

        // Segundo OrderCompleted -> el ApplyDelivered es no-op (ya esta Delivered) y NO debe pushear de nuevo.
        await Harness.Bus.Publish(new OrderCompletedIntegrationEvent
        {
            OrderId = orderId, CustomerUserId = Guid.NewGuid(), MerchantId = Guid.NewGuid(), CompletedAtUtc = completedAt,
        });
        await Harness.GetConsumerHarness<OrderTerminalEventsConsumer>()
            .Consumed.Any<OrderCompletedIntegrationEvent>(f => f.Context.Message.OrderId == orderId);

        await Notifier.DidNotReceive().PushStatus(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrderCancelled_StoresReason_DeletesMapping_PushesStatus()
    {
        Guid orderId = Guid.CreateVersion7();
        Guid courierId = Guid.CreateVersion7();
        DateTime cancelledAt = DateTime.UtcNow;
        const string Reason = "Merchant rejected";

        await SeedAssignedAsync(orderId, courierId);

        await Harness.Bus.Publish(new OrderCancelledIntegrationEvent
        {
            OrderId = orderId, Reason = Reason, CancelledAtUtc = cancelledAt,
        });
        await Harness.GetConsumerHarness<OrderTerminalEventsConsumer>()
            .Consumed.Any<OrderCancelledIntegrationEvent>(f => f.Context.Message.OrderId == orderId);

        (IServiceScope scope, TrackingDbContext db) = CreateDbScope();
        using (scope)
        {
            OrderTracking? tracking = await db.OrderTrackings.AsNoTracking().FirstOrDefaultAsync(t => t.Id == orderId);
            tracking!.CurrentStatus.Should().Be(TrackingStatus.Cancelled);
            tracking.StatusReason.Should().Be(Reason);

            int mappings = await db.CourierActiveOrders.AsNoTracking().CountAsync(m => m.OrderId == orderId);
            mappings.Should().Be(0);
        }

        await Notifier.Received(1).PushStatus(
            orderId, nameof(TrackingStatus.Cancelled), cancelledAt, Reason, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrderFailed_StoresReason_PushesStatus()
    {
        Guid orderId = Guid.CreateVersion7();
        const string Reason = "Stock unavailable";

        await SeedPlacedAsync(orderId, customerUserId: Guid.NewGuid());

        await Harness.Bus.Publish(new OrderFailedIntegrationEvent
        {
            OrderId = orderId, Reason = Reason, FailedAtUtc = DateTime.UtcNow,
        });
        await Harness.GetConsumerHarness<OrderTerminalEventsConsumer>()
            .Consumed.Any<OrderFailedIntegrationEvent>(f => f.Context.Message.OrderId == orderId);

        (IServiceScope scope, TrackingDbContext db) = CreateDbScope();
        using (scope)
        {
            OrderTracking? tracking = await db.OrderTrackings.AsNoTracking().FirstOrDefaultAsync(t => t.Id == orderId);
            tracking!.CurrentStatus.Should().Be(TrackingStatus.Failed);
            tracking.StatusReason.Should().Be(Reason);
        }

        await Notifier.Received(1).PushStatus(
            orderId, nameof(TrackingStatus.Failed), Arg.Any<DateTime>(), Reason, Arg.Any<CancellationToken>());
    }

    private async Task SeedPlacedAsync(Guid orderId, Guid customerUserId)
    {
        (IServiceScope scope, TrackingDbContext db) = CreateDbScope();
        using (scope)
        {
            OrderTracking tracking = OrderTracking.FromOrderSubmitted(
                orderId, customerUserId, Guid.NewGuid(),
                18.48, -69.93, 18.49, -69.94,
                DateTime.UtcNow.AddMinutes(-5));
            db.OrderTrackings.Add(tracking);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);
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
