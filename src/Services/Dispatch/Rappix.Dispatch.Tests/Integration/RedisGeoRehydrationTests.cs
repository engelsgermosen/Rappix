using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Dispatch.Application;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Domain.Couriers;
using Rappix.Dispatch.Infrastructure.Persistence;
using Rappix.Dispatch.Infrastructure.Redis;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Rappix.Dispatch.Tests.Integration;

/// <summary>
/// Prueba que el RedisGeoRehydrationService rellena el geo set desde la BD cuando arranca sin
/// estado en Redis (caso flush / restart sin persistencia). Sin el hosted service, el primer
/// CourierRequested tras un flush devolveria CourierUnavailable a pesar de tener couriers Online.
/// </summary>
public sealed class RedisGeoRehydrationTests : IAsyncLifetime
{
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

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        var services = new ServiceCollection();
        services.AddLogging();
        // AddDispatchApplication registra MediatR (DispatchDbContext lo inyecta para domain events).
        // AddMassTransitTestHarness provee IPublishEndpoint stub para los DomainEventHandlers.
        services.AddDispatchApplication();
        services.AddMassTransitTestHarness();
        services.AddDbContext<DispatchDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DispatchDbContext>());
        services.AddScoped<ICourierRepository, CourierRepository>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        _redisClient = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        services.AddSingleton<IConnectionMultiplexer>(_redisClient);
        services.AddScoped<IRedisGeoIndex, RedisGeoIndex>();

        _provider = services.BuildServiceProvider(validateScopes: true);

        using IServiceScope scope = _provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DispatchDbContext>().Database.MigrateAsync();
    }

    [Fact]
    public async Task Rehydration_LoadsAllOnlineCouriersWithLocation()
    {
        // Seed 3 couriers en BD: 1 Online con location (deberia entrar), 1 Online sin location
        // (no entra hasta primer report), 1 Offline (no entra).
        Guid onlineWithLocId = Guid.CreateVersion7();
        Guid onlineNoLocId = Guid.CreateVersion7();
        Guid offlineId = Guid.CreateVersion7();
        DateTime now = DateTime.UtcNow;

        using (IServiceScope scope = _provider.CreateScope())
        {
            DispatchDbContext db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();

            CourierProfile online1 = CourierProfile.CreateDraft(CourierId.FromUserId(onlineWithLocId), "Online1", now);
            online1.SetVehicle(Vehicle.Create(VehicleType.Moto, "A1", capacityKg: 10m).Value, now);
            online1.ReportLocation(18.4861, -69.9312, now);
            online1.GoOnline(now);
            db.CourierProfiles.Add(online1);

            CourierProfile online2 = CourierProfile.CreateDraft(CourierId.FromUserId(onlineNoLocId), "Online2", now);
            online2.SetVehicle(Vehicle.Create(VehicleType.Moto, "A2", capacityKg: 10m).Value, now);
            online2.GoOnline(now);
            db.CourierProfiles.Add(online2);

            CourierProfile offline = CourierProfile.CreateDraft(CourierId.FromUserId(offlineId), "Offline", now);
            db.CourierProfiles.Add(offline);

            await db.SaveChangesAsync();
        }

        // Verificar que Redis arranca vacio.
        IDatabase database = _redisClient.GetDatabase();
        long countBefore = await database.SortedSetLengthAsync("dispatch:couriers:geo");
        countBefore.Should().Be(0);

        // Ejecutar el hosted service.
        var rehydration = ActivatorUtilities.CreateInstance<RedisGeoRehydrationService>(_provider, NullLogger<RedisGeoRehydrationService>.Instance);
        await rehydration.StartAsync(CancellationToken.None);

        // Solo el courier Online con location entra al geo set.
        long countAfter = await database.SortedSetLengthAsync("dispatch:couriers:geo");
        countAfter.Should().Be(1);

        GeoPosition? pos = (await database.GeoPositionAsync("dispatch:couriers:geo", onlineWithLocId.ToString()));
        pos.Should().NotBeNull();
        pos!.Value.Latitude.Should().BeApproximately(18.4861, 0.0001);

        // Online sin location y Offline NO entran.
        (await database.GeoPositionAsync("dispatch:couriers:geo", onlineNoLocId.ToString())).Should().BeNull();
        (await database.GeoPositionAsync("dispatch:couriers:geo", offlineId.ToString())).Should().BeNull();
    }

    public async Task DisposeAsync()
    {
        await _redisClient.DisposeAsync();
        await _provider.DisposeAsync();
        await _redis.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
