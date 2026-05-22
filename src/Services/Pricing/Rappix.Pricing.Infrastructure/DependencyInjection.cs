using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.BuildingBlocks.Messaging.Extensions;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Infrastructure.Grpc;
using Rappix.Pricing.Infrastructure.Grpc.Catalog;
using Rappix.Pricing.Infrastructure.Grpc.Merchants;
using Rappix.Pricing.Infrastructure.Messaging;
using Rappix.Pricing.Infrastructure.Persistence;
using Rappix.Pricing.Infrastructure.Persistence.Repositories;

namespace Rappix.Pricing.Infrastructure;

/// <summary>Registro de servicios de la capa de infraestructura.</summary>
public static class DependencyInjection
{
    /// <summary>Registra el DbContext, repositorios, clientes gRPC (Catalog + Merchants) y mensajeria con outbox + consumer.</summary>
    public static IServiceCollection AddPricingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("PricingDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexion 'PricingDb'.");

        services.AddDbContext<PricingDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<PricingDbContext>());
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<ICouponRepository, CouponRepository>();
        services.AddScoped<ISurgeRuleRepository, SurgeRuleRepository>();
        services.AddScoped<IItemPriceCacheRepository, ItemPriceCacheRepository>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        AddDistributedCache(services, configuration);
        AddCatalogGrpcClient(services, configuration);
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

    private static void AddCatalogGrpcClient(IServiceCollection services, IConfiguration configuration)
    {
        string catalogUrl = configuration["Grpc:CatalogUrl"] ?? "http://localhost:8081";

        services.AddGrpcClient<CatalogValidationService.CatalogValidationServiceClient>(options =>
                options.Address = new Uri(catalogUrl))
            .AddStandardResilienceHandler();

        services.AddScoped<ICatalogPricingClient, CatalogPricingGrpcClient>();
    }

    private static void AddMerchantsGrpcClient(IServiceCollection services, IConfiguration configuration)
    {
        string merchantsUrl = configuration["Grpc:MerchantsUrl"] ?? "http://localhost:8081";

        services.AddGrpcClient<MerchantValidationService.MerchantValidationServiceClient>(options =>
                options.Address = new Uri(merchantsUrl))
            .AddStandardResilienceHandler();

        services.AddScoped<IMerchantPricingClient, MerchantPricingGrpcClient>();
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration) =>
        services.AddRappixMessaging(configuration, serviceName: "pricing", configure: bus =>
        {
            bus.AddConsumer<ItemCreatedConsumer>();
            bus.AddEntityFrameworkOutbox<PricingDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });
        });
}
