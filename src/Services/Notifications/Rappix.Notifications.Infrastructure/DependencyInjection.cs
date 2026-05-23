using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Infrastructure.Channels;
using Rappix.Notifications.Infrastructure.Persistence;
using Rappix.Notifications.Infrastructure.Persistence.Repositories;

namespace Rappix.Notifications.Infrastructure;

/// <summary>Registro de servicios de la capa de infraestructura.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra el DbContext, los 4 repositorios, el reloj, el <see cref="INotificationChannel"/>
    /// conmutable (Fake/SendGrid) y (en commit 6) MassTransit con outbox callback. El switch del
    /// canal sigue el patron de <c>IPaymentGateway</c> en Payments: Fake por defecto;
    /// <c>Notifications:Channel=SendGrid</c> opt-in en commit 9.
    /// </summary>
    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("NotificationsDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexion 'NotificationsDb'.");

        services.AddDbContext<NotificationsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<NotificationsDbContext>());

        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IUserContactRepository, UserContactRepository>();
        services.AddScoped<IMerchantContactRepository, MerchantContactRepository>();
        services.AddScoped<INotificationOrderRepository, NotificationOrderRepository>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        RegisterNotificationChannel(services, configuration);

        return services;
    }

    private static void RegisterNotificationChannel(IServiceCollection services, IConfiguration configuration)
    {
        // Solo leemos el string suelto (no requiere Microsoft.Extensions.Configuration.Binder en
        // Infrastructure; el bind de NotificationsOptions completo lo hace Api en Program.cs).
        string channel = configuration["Notifications:Channel"] ?? "Fake";

        if (string.Equals(channel, "SendGrid", StringComparison.OrdinalIgnoreCase))
        {
            // SendGridNotificationChannel se cablea en el commit 9 de Fase 9. Por ahora fail-fast
            // explicito si alguien fuerza el switch en config — evita arrancar con un canal "fantasma"
            // (sin la dependencia SendGrid en csproj el resolve fallaria mas tarde con un mensaje
            // menos claro).
            throw new InvalidOperationException(
                "Notifications:Channel=SendGrid no esta cableado hasta el commit 9. Use Notifications:Channel=Fake.");
        }

        // FakeNotificationChannel como Singleton: no mantiene estado (a diferencia de
        // FakePaymentGateway que cachea idempotency keys); solo loguea. Singleton ahorra
        // el resolve por scope sin coste de aislamiento.
        services.AddSingleton<INotificationChannel, FakeNotificationChannel>();
    }
}
