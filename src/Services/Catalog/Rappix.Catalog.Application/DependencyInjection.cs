using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Rappix.Catalog.Application.Behaviors;

namespace Rappix.Catalog.Application;

/// <summary>Registro de servicios de la capa de aplicacion.</summary>
public static class DependencyInjection
{
    /// <summary>Registra MediatR, los validadores de FluentValidation y el pipeline de validacion.</summary>
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        return services;
    }
}
