using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Rappix.Notifications.Application.Notifications;

namespace Rappix.Notifications.Application;

/// <summary>Registro de servicios de la capa de aplicacion.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra MediatR, FluentValidation y el orquestador <see cref="INotifyHandler"/>. Los
    /// handlers concretos (consumers) se registran en Infrastructure via <c>AddRappixMessaging</c>.
    /// </summary>
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // Scoped: el handler usa IUnitOfWork (scoped vinculado al DbContext del scope MassTransit).
        services.AddScoped<INotifyHandler, NotifyHandler>();

        return services;
    }
}
