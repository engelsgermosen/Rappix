using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.BuildingBlocks.Messaging.Extensions;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Application.Sagas;
using Rappix.Orders.Infrastructure.Grpc;
using Rappix.Orders.Infrastructure.Grpc.Catalog;
using Rappix.Orders.Infrastructure.Grpc.Merchants;
using Rappix.Orders.Infrastructure.Grpc.Pricing;
using Rappix.Orders.Infrastructure.Messaging.Activities;
using Rappix.Orders.Infrastructure.Messaging.Projections;
using Rappix.Orders.Infrastructure.Messaging.Simulation;
using Rappix.Orders.Infrastructure.Persistence;
using Rappix.Orders.Infrastructure.Persistence.Repositories;

namespace Rappix.Orders.Infrastructure;

/// <summary>Registro de servicios de la capa de infraestructura del servicio Orders.</summary>
public static class DependencyInjection
{
    /// <summary>Registra DbContext, repositorios, clientes gRPC, scheduler Quartz y la mensajeria con la saga + outbox.</summary>
    public static IServiceCollection AddOrdersInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("OrdersDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexion 'OrdersDb'.");

        services.AddDbContext<OrdersDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<OrdersDbContext>());
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        AddDistributedCache(services, configuration);
        AddPricingGrpcClient(services, configuration);
        AddStockReservationGrpcClient(services, configuration);
        AddMerchantsGrpcClient(services, configuration);
        AddScheduler(services, connectionString);
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
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
        }
    }

    private static void AddPricingGrpcClient(IServiceCollection services, IConfiguration configuration)
    {
        string pricingUrl = configuration["Grpc:PricingUrl"] ?? "http://localhost:8081";
        services.AddGrpcClient<PricingService.PricingServiceClient>(options => options.Address = new Uri(pricingUrl))
            .AddStandardResilienceHandler();
        services.AddScoped<IPricingClient, PricingGrpcClient>();
    }

    private static void AddStockReservationGrpcClient(IServiceCollection services, IConfiguration configuration)
    {
        string catalogUrl = configuration["Grpc:CatalogUrl"] ?? "http://localhost:8081";
        services.AddGrpcClient<StockReservationService.StockReservationServiceClient>(options => options.Address = new Uri(catalogUrl))
            .AddStandardResilienceHandler();
        services.AddScoped<IStockReservationClient, StockReservationGrpcClient>();
    }

    private static void AddMerchantsGrpcClient(IServiceCollection services, IConfiguration configuration)
    {
        string merchantsUrl = configuration["Grpc:MerchantsUrl"] ?? "http://localhost:8081";
        services.AddGrpcClient<MerchantValidationService.MerchantValidationServiceClient>(options => options.Address = new Uri(merchantsUrl))
            .AddStandardResilienceHandler();
        services.AddScoped<IMerchantValidationClient, MerchantValidationGrpcClient>();
    }

    private static void AddScheduler(IServiceCollection services, string connectionString)
    {
        // Quartz con job store Postgres: los timeouts de la saga sobreviven reinicios (a diferencia del
        // delayed exchange de RabbitMQ, ausente en la imagen pinned y no durable). Tablas QRTZ_* en la migracion.
        services.AddQuartz(quartz =>
        {
            quartz.UsePersistentStore(store =>
            {
                store.UseProperties = true;
                store.UsePostgres(connectionString);
                store.UseNewtonsoftJsonSerializer();
            });
        });
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration)
    {
        bool enableSimulated = configuration.GetValue("Orders:EnableSimulatedResponders", defaultValue: true);

        services.AddRappixMessaging(
            configuration,
            serviceName: "orders",
            configure: bus =>
            {
                bus.AddPublishMessageScheduler();
                bus.AddQuartzConsumers();

                bus.AddSagaStateMachine<OrderStateMachine, OrderState>()
                    .EntityFrameworkRepository(repository =>
                    {
                        repository.ConcurrencyMode = ConcurrencyMode.Optimistic;
                        repository.ExistingDbContext<OrdersDbContext>();
                        repository.UsePostgres();
                    });

                // Activities (efectos gRPC) + proyeccion del estado al agregado.
                bus.AddConsumer<ConsumeQuoteConsumer>();
                bus.AddConsumer<ReserveStockConsumer>();
                bus.AddConsumer<CommitStockConsumer>();
                bus.AddConsumer<ReleaseStockConsumer>();
                bus.AddConsumer<RevertQuoteConsumer>();
                bus.AddConsumer<OrderStatusProjectionConsumer>();

                // Responders simulados de pago/courier/entrega (borrables cuando lleguen los servicios reales).
                if (enableSimulated)
                {
                    bus.AddConsumer<SimulatedPaymentResponder>();
                    bus.AddConsumer<SimulatedCourierResponder>();
                    bus.AddConsumer<SimulatedDeliveryResponder>();
                }

                bus.AddEntityFrameworkOutbox<OrdersDbContext>(outbox =>
                {
                    outbox.UsePostgres();
                    outbox.UseBusOutbox();
                });
            },
            configureBus: (_, cfg) => cfg.UsePublishMessageScheduler());
    }
}
