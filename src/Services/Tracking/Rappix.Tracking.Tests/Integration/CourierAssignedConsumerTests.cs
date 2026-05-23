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

public sealed class CourierAssignedConsumerTests : TrackingConsumerTestBase
{
    protected override void ConfigureHarness(IBusRegistrationConfigurator configurator) =>
        configurator.AddConsumer<CourierAssignedConsumer>();

    [Fact]
    public async Task Consume_CreatesMapping_UpdatesTracking_PushesStatus()
    {
        Guid orderId = Guid.CreateVersion7();
        Guid courierId = Guid.CreateVersion7();
        await SeedPlacedAsync(orderId, customerUserId: Guid.NewGuid());

        await Harness.Bus.Publish(new CourierAssignedIntegrationEvent { OrderId = orderId, CourierId = courierId });

        (await Harness.GetConsumerHarness<CourierAssignedConsumer>()
            .Consumed.Any<CourierAssignedIntegrationEvent>(f => f.Context.Message.OrderId == orderId))
            .Should().BeTrue();

        (IServiceScope scope, TrackingDbContext db) = CreateDbScope();
        using (scope)
        {
            OrderTracking? tracking = await db.OrderTrackings.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == orderId);
            tracking!.CurrentStatus.Should().Be(TrackingStatus.CourierAssigned);
            tracking.LastCourierId.Should().Be(courierId);

            CourierActiveOrder? mapping = await db.CourierActiveOrders.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == courierId);
            mapping!.OrderId.Should().Be(orderId);
        }

        await Notifier.Received(1).PushStatus(
            orderId, nameof(TrackingStatus.CourierAssigned), Arg.Any<DateTime>(), null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_ReassignsSameCourierToDifferentOrder_UpdatesMappingRow()
    {
        // Carrera: el primer pedido aun tiene mapping vivo (terminal aun no procesado) cuando
        // llega CourierAssigned de un segundo pedido. El upsert refleja la nueva asignacion.
        Guid courierId = Guid.CreateVersion7();
        Guid orderA = Guid.CreateVersion7();
        Guid orderB = Guid.CreateVersion7();

        await SeedPlacedAsync(orderA, customerUserId: Guid.NewGuid());
        await SeedPlacedAsync(orderB, customerUserId: Guid.NewGuid());
        await SeedMappingAsync(courierId, orderA);

        await Harness.Bus.Publish(new CourierAssignedIntegrationEvent { OrderId = orderB, CourierId = courierId });
        await Harness.GetConsumerHarness<CourierAssignedConsumer>()
            .Consumed.Any<CourierAssignedIntegrationEvent>(f => f.Context.Message.OrderId == orderB);

        (IServiceScope scope, TrackingDbContext db) = CreateDbScope();
        using (scope)
        {
            CourierActiveOrder? mapping = await db.CourierActiveOrders.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == courierId);
            mapping!.OrderId.Should().Be(orderB);

            // Solo deberia haber UNA fila para este courier.
            int count = await db.CourierActiveOrders.AsNoTracking().CountAsync(m => m.Id == courierId);
            count.Should().Be(1);
        }
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

    private async Task SeedMappingAsync(Guid courierId, Guid orderId)
    {
        (IServiceScope scope, TrackingDbContext db) = CreateDbScope();
        using (scope)
        {
            db.CourierActiveOrders.Add(CourierActiveOrder.Create(courierId, orderId, DateTime.UtcNow.AddMinutes(-1)));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);
        }
    }
}
