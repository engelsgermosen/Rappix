using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Rappix.Dispatch.Application.Behaviors;
using Rappix.Dispatch.Application.Couriers.Assignment;

namespace Rappix.Dispatch.Application;

/// <summary>Registro de servicios de la capa de aplicacion.</summary>
public static class DependencyInjection
{
    /// <summary>Registra MediatR, los validadores, el pipeline de validacion y la strategy de asignacion.</summary>
    public static IServiceCollection AddDispatchApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // Strategy de asignacion: una sola para Fase 6 (NearestAvailable, internal — registrada
        // desde aqui porque Infrastructure no la ve). Swap futuro a OfferBased via cambio de
        // registro DI sin tocar el orchestrator (CourierRequestedConsumer).
        services.AddScoped<ICourierAssignmentStrategy, NearestAvailableStrategy>();

        return services;
    }
}
