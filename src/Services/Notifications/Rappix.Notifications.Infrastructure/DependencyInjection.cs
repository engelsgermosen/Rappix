using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Infrastructure.Persistence;
using Rappix.Notifications.Infrastructure.Persistence.Repositories;

namespace Rappix.Notifications.Infrastructure;

/// <summary>Registro de servicios de la capa de infraestructura.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra el DbContext, los 4 repositorios, el reloj y (en commits posteriores) el switch
    /// del <c>INotificationChannel</c> + MassTransit con outbox callback. El stub actual cubre solo
    /// persistencia para que commit 4 quede atomico; el wiring de canal y mensajeria llega en
    /// commits 5 y 6.
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

        return services;
    }
}
