using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.BuildingBlocks.Imaging;
using Rappix.BuildingBlocks.Messaging.Extensions;
using Rappix.BuildingBlocks.Storage;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Infrastructure.Grpc;
using Rappix.Catalog.Infrastructure.Grpc.Merchants;
using Rappix.Catalog.Infrastructure.Messaging;
using Rappix.Catalog.Infrastructure.Persistence;
using Rappix.Catalog.Infrastructure.Persistence.Repositories;

namespace Rappix.Catalog.Infrastructure;

/// <summary>Registro de servicios de la capa de infraestructura.</summary>
public static class DependencyInjection
{
    /// <summary>Registra el DbContext, repositorios, almacenamiento/imagen, cliente gRPC de Merchants y mensajeria con outbox + consumers.</summary>
    public static IServiceCollection AddCatalogInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("CatalogDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexion 'CatalogDb'.");

        services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<CatalogDbContext>());
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IStockReservationRepository, StockReservationRepository>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Storage e imagen son building blocks compartidos (regla de tres). Bucket via Minio:Bucket=catalog-items.
        services.AddRappixImageValidator();
        services.AddRappixObjectStorage(configuration);

        AddDistributedCache(services, configuration);
        AddMerchantsGrpcClient(services, configuration);
        AddMessaging(services, configuration);

        return services;
    }

    private static void AddDistributedCache(IServiceCollection services, IConfiguration configuration)
    {
        // La idempotencia del middleware REST necesita IDistributedCache. Redis si esta configurado; memoria si no.
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

    private static void AddMerchantsGrpcClient(IServiceCollection services, IConfiguration configuration)
    {
        string merchantsUrl = configuration["Grpc:MerchantsUrl"] ?? "http://localhost:8081";

        // Cliente gRPC + resiliencia estandar (retry + circuit breaker via Polly).
        services.AddGrpcClient<MerchantValidationService.MerchantValidationServiceClient>(options =>
                options.Address = new Uri(merchantsUrl))
            .AddStandardResilienceHandler();

        services.AddScoped<IMerchantValidationClient, MerchantValidationGrpcClient>();
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration) =>
        services.AddRappixMessaging(configuration, serviceName: "catalog", configure: bus =>
        {
            bus.AddConsumer<MerchantApprovedConsumer>();
            bus.AddConsumer<MerchantActivatedConsumer>();
            bus.AddConsumer<MerchantSuspendedConsumer>();
            bus.AddEntityFrameworkOutbox<CatalogDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });
        });
}
