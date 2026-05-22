using Microsoft.Extensions.Options;
using Rappix.Pricing.Application.Configuration;
using Rappix.Pricing.Domain.Common;

namespace Rappix.Pricing.Application.Pricing.Delivery;

/// <summary>
/// Selecciona la estrategia de envio que aplica al vertical. Si ninguna lo maneja (vertical futuro),
/// hace fallback a la tarifa estandar de configuracion.
/// </summary>
public sealed class DeliveryFeeResolver(IEnumerable<IDeliveryFeeStrategy> strategies, IOptions<PricingOptions> options)
{
    /// <summary>Resuelve la tarifa de envio para un vertical y una distancia (km).</summary>
    public decimal Resolve(VerticalType vertical, decimal distanceKm)
    {
        IDeliveryFeeStrategy? strategy = strategies.FirstOrDefault(candidate => candidate.Handles(vertical));
        if (strategy is not null)
        {
            return strategy.Calculate(distanceKm);
        }

        DeliveryRate fallback = options.Value.Delivery.Standard;
        return fallback.BaseFee + fallback.PerKm * Math.Max(0m, distanceKm);
    }
}
