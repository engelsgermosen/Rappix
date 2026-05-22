using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Rappix.Pricing.Application.Behaviors;
using Rappix.Pricing.Application.Pricing.Delivery;
using Rappix.Pricing.Application.Pricing.Discounts;
using Rappix.Pricing.Application.Pricing.Surge;

namespace Rappix.Pricing.Application;

/// <summary>Registro de servicios de la capa de aplicacion.</summary>
public static class DependencyInjection
{
    /// <summary>Registra MediatR, los validadores, el pipeline de validacion y el motor de pricing (calculador + estrategias).</summary>
    public static IServiceCollection AddPricingApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // Motor de pricing. El calculador es una funcion pura estatica (QuoteCalculator); las estrategias
        // pueden tocar repositorios (scoped). El orquestador compone las estrategias en orden determinístico.
        services.AddScoped<ISurgeStrategy, ConfigurableSurgeStrategy>();
        services.AddScoped<IDeliveryFeeStrategy, StandardDeliveryFeeStrategy>();
        services.AddScoped<IDeliveryFeeStrategy, ParcelDeliveryFeeStrategy>();
        services.AddScoped<DeliveryFeeResolver>();
        services.AddScoped<IDiscountRule, FirstOrderDiscountRule>();
        services.AddScoped<IDiscountRule, CouponDiscountRule>();

        return services;
    }
}
