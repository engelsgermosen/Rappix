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

                // Responder simulado de ENTREGA (borrable cuando un servicio real publique
                // OrderDeliveredIntegrationEvent — la propia Fase 6 dejo Dispatch.OrderTerminalEventsConsumer
                // como el consumidor, pero el "delivered" lo dispara hoy el endpoint mark-delivered + este
                // responder). El responder de COURIER se borro en Fase 6 (Dispatch responde
                // CourierRequested con un courier real). El responder de PAGO se borro en Fase 8
                // (Payments consume PaymentRequested y publica PaymentSucceeded/Failed real).
                if (enableSimulated)
                {
                    bus.AddConsumer<SimulatedDeliveryResponder>();
                }

                bus.AddEntityFrameworkOutbox<OrdersDbContext>(outbox =>
                {
                    outbox.UsePostgres();
                    outbox.UseBusOutbox();
                });

                // CRITICO: aplica el filtro de outbox EF a CADA endpoint (consumers + saga). Sin esto, los
                // consumers "activity" y los responders simulados —que NO escriben en el DbContext— publican su
                // evento-resultado (p. ej. QuoteConsumed) a traves del bus outbox, que lo bufferiza hasta un
                // SaveChanges que nunca ocurre: el mensaje se PIERDE y la saga se atasca (sin error ni fault).
                // El filtro por endpoint envuelve el consume en transaccion y hace el SaveChanges que vacia el
                // buffer (y aporta dedup de inbox). La saga ya funcionaba porque su SaveChanges persiste OrderState.
                bus.AddConfigureEndpointsCallback((context, _, cfg) => cfg.UseEntityFrameworkOutbox<OrdersDbContext>(context));
            },
            configureBus: (_, cfg) => cfg.UsePublishMessageScheduler());
    }
}
