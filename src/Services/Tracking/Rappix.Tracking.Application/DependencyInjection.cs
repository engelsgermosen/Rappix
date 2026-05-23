using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Rappix.Tracking.Application;

/// <summary>Registro de servicios de la capa de aplicacion.</summary>
public static class DependencyInjection
{
    /// <summary>Registra MediatR y los validadores. Los handlers concretos se incorporan en commits posteriores.</summary>
    public static IServiceCollection AddTrackingApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        return services;
    }
}
