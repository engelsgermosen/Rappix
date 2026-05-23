using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.BuildingBlocks.Messaging.Extensions;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Infrastructure.Messaging;
using Rappix.Dispatch.Infrastructure.Persistence;

namespace Rappix.Dispatch.Infrastructure;

/// <summary>Registro de servicios de la capa de infraestructura.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra el DbContext, repositorios, mensajeria con outbox + AddConfigureEndpointsCallback
    /// (CRITICO para el outbox flush — leccion de Orders) y la cache distribuida para la
    /// idempotencia REST. El cliente Redis (geo matching) y la implementacion de IRedisGeoIndex
    /// se anaden en commit 9 cuando aparecen los handlers que la usan.
    /// </summary>
    public static IServiceCollection AddDispatchInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("DispatchDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexion 'DispatchDb'.");

        services.AddDbContext<DispatchDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DispatchDbContext>());
        services.AddScoped<ICourierRepository, CourierRepository>();
        services.AddScoped<ICourierAssignmentRepository, CourierAssignmentRepository>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        AddDistributedCache(services, configuration);
        AddMessaging(services, configuration);

        return services;
    }

    private static void AddDistributedCache(IServiceCollection services, IConfiguration configuration)
    {
        // La idempotencia del middleware REST necesita IDistributedCache (sino el middleware 500ea
        // todas las requests autorizadas — leccion de Catalog).
        string? redisConnection = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
        }
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration) =>
        services.AddRappixMessaging(configuration, serviceName: "dispatch", configure: bus =>
        {
            // Choreography de Identity -> Dispatch (filtra UserType="Courier").
            bus.AddConsumer<UserRegisteredConsumer>();

            bus.AddEntityFrameworkOutbox<DispatchDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });

            // CRITICO (leccion de Orders): aplica el filtro de outbox EF a CADA endpoint. Sin esto, los
            // consumers que publican pero no escriben en el DbContext (commit 11 CourierRequestedConsumer
            // publicara CourierAssigned/Unavailable) bufferizan el publish en el outbox y nunca hacen
            // SaveChanges -> el mensaje se pierde silenciosamente. El filtro envuelve cada consume en
            // transaccion + SaveChanges y aporta dedup de inbox.
            bus.AddConfigureEndpointsCallback((context, _, cfg) => cfg.UseEntityFrameworkOutbox<DispatchDbContext>(context));
        });
}
