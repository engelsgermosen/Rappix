using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.BuildingBlocks.Messaging.Extensions;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Infrastructure.Imaging;
using Rappix.Merchants.Infrastructure.Messaging;
using Rappix.Merchants.Infrastructure.Persistence;
using Rappix.Merchants.Infrastructure.Persistence.Repositories;
using Rappix.Merchants.Infrastructure.Storage;

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
        services.AddSingleton<IImageValidator, ImageSharpImageValidator>();

        AddObjectStorage(services);
        AddDistributedCache(services, configuration);
        AddMessaging(services, configuration);

        return services;
    }

    // MinioOptions se vincula en Program.cs (capa Api), igual que JwtOptions/SendGridOptions en Identity.
    private static void AddObjectStorage(IServiceCollection services)
    {
        services.AddSingleton<IMinioClient>(provider =>
        {
            MinioOptions options = provider.GetRequiredService<IOptions<MinioOptions>>().Value;
            return new MinioClient()
                .WithEndpoint(options.Endpoint)
                .WithCredentials(options.AccessKey, options.SecretKey)
                .WithSSL(options.UseSsl)
                .Build();
        });

        services.AddSingleton<IObjectStorage, MinioObjectStorage>();
        services.AddHostedService<MinioBucketInitializer>();
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
