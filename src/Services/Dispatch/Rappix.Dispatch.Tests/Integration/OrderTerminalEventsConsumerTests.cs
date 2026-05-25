using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Dispatch;
using Rappix.Contracts.Orders;
using Rappix.Dispatch.Application;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Application.Configuration;
using Rappix.Dispatch.Domain.Couriers;
using Rappix.Dispatch.Infrastructure.Messaging;
using Rappix.Dispatch.Infrastructure.Persistence;
using Rappix.Dispatch.Infrastructure.Redis;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Rappix.Dispatch.Tests.Integration;

/// <summary>
/// Cubre el flujo completo: place CourierRequested -> consumer asigna -> luego publica un terminal
/// (Delivered/Cancelled/Failed) -> OrderTerminalEventsConsumer libera el courier (Busy -> Online +
/// assignment cerrada + de vuelta a Redis Geo si conocia su LastLocation).
/// </summary>
public sealed class OrderTerminalEventsConsumerTests : IAsyncLifetime
{
    private const double Pickup_Lat = 18.4861;
    private const double Pickup_Lng = -69.9312;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("rappix_dispatch")
        .WithUsername("rappix")
        .WithPassword("rappix_test")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private ServiceProvider _provider = null!;
    private ConnectionMultiplexer _redisClient = null!;

    private ITestHarness Harness => _provider.GetRequiredService<ITestHarness>();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDispatchApplication();
        services.AddDbContext<DispatchDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DispatchDbContext>());
        services.AddScoped<ICourierRepository, CourierRepository>();
        services.AddScoped<ICourierAssignmentRepository, CourierAssignmentRepository>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.Configure<DispatchOptions>(options =>
        {
            options.Matching.RadiusMeters = 5000d;
            options.Matching.CandidateLimit = 10;
        });
        _redisClient = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        services.AddSingleton<IConnectionMultiplexer>(_redisClient);
        services.AddScoped<IRedisGeoIndex, RedisGeoIndex>();
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.AddConsumer<CourierRequestedConsumer>();
            configurator.AddConsumer<OrderTerminalEventsConsumer>();
        });

        _provider = services.BuildServiceProvider(validateScopes: true);

        using (IServiceScope scope = _provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DispatchDbContext>().Database.MigrateAsync();
        }

        await Harness.Start();
    }

    [Fact]
    public async Task PlaceAssignDeliver_E2E_ReleasesCourier_BackToOnline_AndRedis()
    {
        // 1) Seed: courier Online + location en BD + Redis Geo.
        CourierId courierId = await SeedOnlineAsync(Pickup_Lat, Pickup_Lng, "E2E");
        Guid orderId = Guid.CreateVersion7();

        // 2) Asignacion: publish CourierRequested -> consumer asigna.
        await Harness.Bus.Publish(new CourierRequestedIntegrationEvent
        {
            OrderId = orderId,
            MerchantId = Guid.CreateVersion7(),
            PickupLatitude = Pickup_Lat,
            PickupLongitude = Pickup_Lng,
            DeliveryLatitude = 18.49,
            DeliveryLongitude = -69.94,
            // Fase 13.6: campos del snapshot.
            CustomerUserId = Guid.CreateVersion7(),
            MerchantName = "Comercio Test",
            DeliveryStreet = "Calle Test 123",
            DeliveryReference = null,
            OrderTotal = 250m,
            OrderCurrency = "DOP",
            Lines = [new Rappix.Contracts.Orders.OrderLineSnapshot("Pizza", 2)],
        });
        await WaitConsumedAsync<CourierRequestedConsumer, CourierRequestedIntegrationEvent>(orderId);

        // Sanity intermedia: courier Busy + asignacion activa + fuera de Redis.
        (await GetCourierAsync(courierId))!.Status.Should().Be(CourierStatus.Busy);
        (await CountActiveAssignmentsForOrderAsync(orderId)).Should().Be(1);
        (await _redisClient.GetDatabase().GeoPositionAsync("dispatch:couriers:geo", courierId.Value.ToString())).Should().BeNull();

        // 3) Entrega: publish OrderDelivered -> consumer libera.
        await Harness.Bus.Publish(new OrderDeliveredIntegrationEvent
        {
            OrderId = orderId,
            DeliveredAtUtc = DateTime.UtcNow,
        });
        await WaitConsumedAsync<OrderTerminalEventsConsumer, OrderDeliveredIntegrationEvent>(orderId);

        // 4) Verificar: courier Online + asignacion cerrada con razon "delivered" + de vuelta a Redis.
        CourierProfile? courier = await GetCourierAsync(courierId);
        courier!.Status.Should().Be(CourierStatus.Online);
        (await CountActiveAssignmentsForOrderAsync(orderId)).Should().Be(0);
        CourierAssignment? closed = await GetAssignmentForOrderAsync(orderId);
        closed!.ReleasedAtUtc.Should().NotBeNull();
        closed.ReleaseReason.Should().Be("delivered");
        (await _redisClient.GetDatabase().GeoPositionAsync("dispatch:couriers:geo", courierId.Value.ToString())).Should().NotBeNull();
    }

    [Fact]
    public async Task OrderCancelled_AfterAssignment_ReleasesCourier()
    {
        CourierId courierId = await SeedOnlineAsync(Pickup_Lat, Pickup_Lng, "Cancel");
        Guid orderId = Guid.CreateVersion7();

        await Harness.Bus.Publish(RequestFor(orderId));
        await WaitConsumedAsync<CourierRequestedConsumer, CourierRequestedIntegrationEvent>(orderId);

        await Harness.Bus.Publish(new OrderCancelledIntegrationEvent
        {
            OrderId = orderId,
            Reason = "Cliente cancelo",
            CancelledAtUtc = DateTime.UtcNow,
        });
        await WaitConsumedAsync<OrderTerminalEventsConsumer, OrderCancelledIntegrationEvent>(orderId);

        (await GetCourierAsync(courierId))!.Status.Should().Be(CourierStatus.Online);
        (await GetAssignmentForOrderAsync(orderId))!.ReleaseReason.Should().Be("cancelled");
    }

    [Fact]
    public async Task OrderFailed_AfterAssignment_ReleasesCourier()
    {
        CourierId courierId = await SeedOnlineAsync(Pickup_Lat, Pickup_Lng, "Fail");
        Guid orderId = Guid.CreateVersion7();

        await Harness.Bus.Publish(RequestFor(orderId));
        await WaitConsumedAsync<CourierRequestedConsumer, CourierRequestedIntegrationEvent>(orderId);

        await Harness.Bus.Publish(new OrderFailedIntegrationEvent
        {
            OrderId = orderId,
            Reason = "Pago tardio",
            FailedAtUtc = DateTime.UtcNow,
        });
        await WaitConsumedAsync<OrderTerminalEventsConsumer, OrderFailedIntegrationEvent>(orderId);

        (await GetCourierAsync(courierId))!.Status.Should().Be(CourierStatus.Online);
        (await GetAssignmentForOrderAsync(orderId))!.ReleaseReason.Should().Be("failed");
    }

    [Fact]
    public async Task Terminal_WithoutAssignment_IsNoOp()
    {
        // El pedido nunca llego a asignarse (cancelado antes de AwaitingCourier): el consumer ignora.
        Guid orderId = Guid.CreateVersion7();

        await Harness.Bus.Publish(new OrderCancelledIntegrationEvent
        {
            OrderId = orderId,
            Reason = "Cliente cancelo antes",
            CancelledAtUtc = DateTime.UtcNow,
        });
        await WaitConsumedAsync<OrderTerminalEventsConsumer, OrderCancelledIntegrationEvent>(orderId);

        // Sin fallos ni asignaciones creadas.
        (await CountActiveAssignmentsForOrderAsync(orderId)).Should().Be(0);
    }

    public async Task DisposeAsync()
    {
        await Harness.Stop();
        await _redisClient.DisposeAsync();
        await _provider.DisposeAsync();
        await _redis.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private async Task<CourierId> SeedOnlineAsync(double latitude, double longitude, string name)
    {
        Guid userId = Guid.CreateVersion7();
        CourierId id = CourierId.FromUserId(userId);
        DateTime now = DateTime.UtcNow;

        using IServiceScope scope = _provider.CreateScope();
        DispatchDbContext db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();

        CourierProfile courier = CourierProfile.CreateDraft(id, name, now);
        courier.SetVehicle(Vehicle.Create(VehicleType.Moto, $"P{userId:N}"[..8], capacityKg: 10m).Value, now);
        courier.ReportLocation(latitude, longitude, now);
        courier.GoOnline(now);
        db.CourierProfiles.Add(courier);
        await db.SaveChangesAsync();

        IRedisGeoIndex geo = scope.ServiceProvider.GetRequiredService<IRedisGeoIndex>();
        await geo.AddOrUpdateAsync(id, latitude, longitude, CancellationToken.None);

        return id;
    }

    private async Task<CourierProfile?> GetCourierAsync(CourierId id)
    {
        using IServiceScope scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DispatchDbContext>()
            .CourierProfiles.FirstOrDefaultAsync(courier => courier.Id == id);
    }

    private async Task<int> CountActiveAssignmentsForOrderAsync(Guid orderId)
    {
        using IServiceScope scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DispatchDbContext>()
            .CourierAssignments.CountAsync(a => a.OrderId == orderId && a.ReleasedAtUtc == null);
    }

    private async Task<CourierAssignment?> GetAssignmentForOrderAsync(Guid orderId)
    {
        using IServiceScope scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DispatchDbContext>()
            .CourierAssignments.FirstOrDefaultAsync(a => a.OrderId == orderId);
    }

    private static CourierRequestedIntegrationEvent RequestFor(Guid orderId) => new()
    {
        OrderId = orderId,
        MerchantId = Guid.CreateVersion7(),
        PickupLatitude = Pickup_Lat,
        PickupLongitude = Pickup_Lng,
        DeliveryLatitude = 18.49,
        DeliveryLongitude = -69.94,
        // Fase 13.6: campos del snapshot.
        CustomerUserId = Guid.CreateVersion7(),
        MerchantName = "Comercio Test",
        DeliveryStreet = "Calle Test 123",
        DeliveryReference = null,
        OrderTotal = 250m,
        OrderCurrency = "DOP",
        Lines = [new Rappix.Contracts.Orders.OrderLineSnapshot("Pizza", 2)],
    };

    private async Task WaitConsumedAsync<TConsumer, TMessage>(Guid orderId)
        where TConsumer : class, IConsumer
        where TMessage : class
    {
        IConsumerTestHarness<TConsumer> consumer = _provider.GetRequiredService<IConsumerTestHarness<TConsumer>>();
        for (int attempt = 0; attempt < 60; attempt++)
        {
            // Filtra por TMessage + match contra el OrderId (todos los terminales y CourierRequested
            // tienen propiedad OrderId — comparamos reflexivamente para reutilizar este helper).
            bool any = consumer.Consumed.Select<TMessage>(received =>
            {
                object? messageOrderId = typeof(TMessage).GetProperty("OrderId")?.GetValue(received.Context.Message);
                return messageOrderId is Guid guid && guid == orderId;
            }).Any();
            if (any)
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"El consumer {typeof(TConsumer).Name} no consumio {typeof(TMessage).Name} para orderId={orderId} dentro de 6s.");
    }
}
