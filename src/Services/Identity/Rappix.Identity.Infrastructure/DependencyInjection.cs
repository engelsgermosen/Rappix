using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.BuildingBlocks.Messaging.Extensions;
using Rappix.Identity.Application.Abstractions;
using Rappix.Identity.Application.Authentication;
using Rappix.Identity.Infrastructure.Authentication;
using Rappix.Identity.Infrastructure.Mail;
using Rappix.Identity.Infrastructure.Persistence;
using Rappix.Identity.Infrastructure.Persistence.Repositories;

namespace Rappix.Identity.Infrastructure;

/// <summary>Registro de servicios de la capa de infraestructura.</summary>
public static class DependencyInjection
{
    /// <summary>Registra el DbContext, repositorios, servicios externos, cache distribuida y mensajeria con outbox.</summary>
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexion 'IdentityDb'.");

        services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<IdentityDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IEmailSender, SendGridEmailSender>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

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
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
        }
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration) =>
        services.AddRappixMessaging(configuration, serviceName: "identity", configure: bus =>
        {
            bus.AddEntityFrameworkOutbox<IdentityDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });
        });
}
