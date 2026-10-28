using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.BuildingBlocks.Imaging;
using Rappix.BuildingBlocks.Messaging.Extensions;
using Rappix.BuildingBlocks.Storage;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Infrastructure.Messaging;
using Rappix.Merchants.Infrastructure.Persistence;
using Rappix.Merchants.Infrastructure.Persistence.Repositories;
using StackExchange.Redis;

namespace Rappix.Merchants.Infrastructure;

/// <summary>Registro de servicios de la capa de infraestructura.</summary>
public static class DependencyInjection
{
    /// <summary>Registra el DbContext (PostGIS), repositorio, almacenamiento de logos, cache distribuida y mensajeria con outbox + consumer.</summary>
    public static IServiceCollection AddMerchantsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("MerchantsDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexion 'MerchantsDb'.");

        services.AddDbContext<MerchantsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite()));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<MerchantsDbContext>());
        services.AddScoped<IMerchantRepository, MerchantRepository>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Storage e imagen ahora son building blocks compartidos (regla de tres).
        services.AddRappixImageValidator();
        services.AddRappixObjectStorage(configuration);

        AddDistributedCache(services, configuration);
        AddMessaging(services, configuration);

        return services;
    }

    private static void AddDistributedCache(IServiceCollection services, IConfiguration configuration)
    {
        string? redisConnection = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            // Una sola instancia (lazy) de IConnectionMultiplexer, compartida entre el cache (via
            // ConnectionMultiplexerFactory) y la instrumentacion OpenTelemetry de Redis
            // (AddRedisInstrumentation la resuelve del contenedor). AddSingleton por si solo no alcanza:
            // AddStackExchangeRedisCache no la toma del DI, hay que pasarle la misma instancia explicitamente.
            var multiplexer = new Lazy<IConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(redisConnection));
            services.AddSingleton(_ => multiplexer.Value);
            services.AddStackExchangeRedisCache(options =>
                options.ConnectionMultiplexerFactory = () => Task.FromResult(multiplexer.Value));
        }
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration) =>
        services.AddRappixMessaging(configuration, serviceName: "merchants", configure: bus =>
        {
            bus.AddConsumer<UserRegisteredConsumer>();
            bus.AddEntityFrameworkOutbox<MerchantsDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });
        });
}
