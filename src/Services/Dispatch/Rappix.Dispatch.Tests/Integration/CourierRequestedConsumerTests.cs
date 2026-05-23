using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Dispatch;
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
/// Pruebas del consumer de asignacion de courier (corazon del servicio). Cada test arranca su propio
/// par de containers (PostgreSQL + Redis) via IAsyncLifetime para garantizar aislamiento total —
/// state cleanup entre tests con IClassFixture probo ser fragil (publishes/consumed accumulan).
/// El coste extra (~10s por test) es aceptable: estos son los tests del CHECKPOINT del claim atomico.
/// </summary>
public sealed class CourierRequestedConsumerTests : IAsyncLifetime
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
        services.AddMassTransitTestHarness(configurator => configurator.AddConsumer<CourierRequestedConsumer>());

        _provider = services.BuildServiceProvider(validateScopes: true);

        using (IServiceScope scope = _provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DispatchDbContext>().Database.MigrateAsync();
        }

        await Harness.Start();
    }

    [Fact]
    public async Task HappyPath_AssignsCourier_PublishesAssigned_MarksBusy_CreatesAssignment_RemovesFromRedis()
    {
        CourierId courierId = await SeedOnlineAsync(Pickup_Lat, Pickup_Lng, "Happy");
        Guid orderId = Guid.CreateVersion7();

        await Harness.Bus.Publish(RequestFor(orderId));
        await WaitForConsumedAsync(orderId);

        // Publish CourierAssigned para este pedido (no Unavailable).
        (await Harness.Published.SelectAsync<CourierAssignedIntegrationEvent>(filter =>
            filter.Context.Message.OrderId == orderId && filter.Context.Message.CourierId == courierId.Value).CountAsync()).Should().Be(1);
        (await Harness.Published.SelectAsync<CourierUnavailableIntegrationEvent>(filter =>
            filter.Context.Message.OrderId == orderId).CountAsync()).Should().Be(0);

        // Courier Busy + assignment activo + fuera de Redis.
        CourierProfile? courier = await GetCourierAsync(courierId);
        courier!.Status.Should().Be(CourierStatus.Busy);
        (await CountActiveAssignmentsForOrderAsync(orderId)).Should().Be(1);
        (await _redisClient.GetDatabase().GeoPositionAsync("dispatch:couriers:geo", courierId.Value.ToString())).Should().BeNull();
    }

    [Fact]
    public async Task NoCouriersInRange_PublishesUnavailable_NoAssignment()
    {
        Guid orderId = Guid.CreateVersion7();

        await Harness.Bus.Publish(RequestFor(orderId));
        await WaitForConsumedAsync(orderId);

        (await Harness.Published.SelectAsync<CourierUnavailableIntegrationEvent>(filter =>
            filter.Context.Message.OrderId == orderId).CountAsync()).Should().Be(1);
        (await Harness.Published.SelectAsync<CourierAssignedIntegrationEvent>(filter =>
            filter.Context.Message.OrderId == orderId).CountAsync()).Should().Be(0);
        (await CountActiveAssignmentsForOrderAsync(orderId)).Should().Be(0);
    }

    [Fact]
    public async Task TwoOrders_OneCourier_ExactlyOneAssigned_OtherUnavailable()
    {
        // El test critico de concurrencia: 2 pedidos compiten por 1 courier; el claim atomico
        // (UPDATE ... WHERE Status='Online') garantiza que solo uno gana.
        CourierId courierId = await SeedOnlineAsync(Pickup_Lat, Pickup_Lng, "Solo");
        Guid orderA = Guid.CreateVersion7();
        Guid orderB = Guid.CreateVersion7();

        await Task.WhenAll(
            Harness.Bus.Publish(RequestFor(orderA)),
            Harness.Bus.Publish(RequestFor(orderB)));

        await WaitForConsumedAsync(orderA);
        await WaitForConsumedAsync(orderB);

        // Asignaciones activas para estos 2 pedidos: exactamente 1 (un courier solo puede tener una;
        // la unique partial index en (courier_id) WHERE released_at_utc IS NULL es la red de seguridad).
        (await CountActiveAssignmentsForOrdersAsync(orderA, orderB)).Should().Be(1);

        // Exactamente 1 CourierAssigned y 1 CourierUnavailable entre los 2 pedidos.
        int assignedCount = await Harness.Published
            .SelectAsync<CourierAssignedIntegrationEvent>(filter =>
                filter.Context.Message.OrderId == orderA || filter.Context.Message.OrderId == orderB)
            .CountAsync();
        int unavailableCount = await Harness.Published
            .SelectAsync<CourierUnavailableIntegrationEvent>(filter =>
                filter.Context.Message.OrderId == orderA || filter.Context.Message.OrderId == orderB)
            .CountAsync();
        assignedCount.Should().Be(1);
        unavailableCount.Should().Be(1);

        // El courier queda Busy.
        CourierProfile? courier = await GetCourierAsync(courierId);
        courier!.Status.Should().Be(CourierStatus.Busy);
    }

    [Fact]
    public async Task Reentrega_NoCreaSegundaAsignacion()
    {
        // Idempotency guard: la reentrega del mismo OrderId encuentra la asignacion activa y es no-op.
        await SeedOnlineAsync(Pickup_Lat, Pickup_Lng, "Idem");
        Guid orderId = Guid.CreateVersion7();

        await Harness.Bus.Publish(RequestFor(orderId));
        await WaitForConsumedAsync(orderId, expected: 1);

        await Harness.Bus.Publish(RequestFor(orderId));
        await WaitForConsumedAsync(orderId, expected: 2);

        // Sigue habiendo exactamente 1 asignacion activa para el pedido.
        (await CountActiveAssignmentsForOrderAsync(orderId)).Should().Be(1);
        // Solo 1 publish de Assigned (la reentrega no publico de nuevo).
        (await Harness.Published.SelectAsync<CourierAssignedIntegrationEvent>(filter =>
            filter.Context.Message.OrderId == orderId).CountAsync()).Should().Be(1);
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

    private async Task<int> CountActiveAssignmentsForOrdersAsync(params Guid[] orderIds)
    {
        using IServiceScope scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DispatchDbContext>()
            .CourierAssignments.CountAsync(a => orderIds.Contains(a.OrderId) && a.ReleasedAtUtc == null);
    }

    private static CourierRequestedIntegrationEvent RequestFor(Guid orderId) => new()
    {
        OrderId = orderId,
        MerchantId = Guid.CreateVersion7(),
        PickupLatitude = Pickup_Lat,
        PickupLongitude = Pickup_Lng,
        DeliveryLatitude = 18.49,
        DeliveryLongitude = -69.94,
    };

    private async Task WaitForConsumedAsync(Guid orderId, int expected = 1)
    {
        IConsumerTestHarness<CourierRequestedConsumer> consumer =
            _provider.GetRequiredService<IConsumerTestHarness<CourierRequestedConsumer>>();
        for (int attempt = 0; attempt < 60; attempt++)
        {
            int consumed = consumer.Consumed
                .Select<CourierRequestedIntegrationEvent>(received => received.Context.Message.OrderId == orderId)
                .Count();
            if (consumed >= expected)
            {
                return;
            }

            await Task.Delay(100);
        }
    }
}
