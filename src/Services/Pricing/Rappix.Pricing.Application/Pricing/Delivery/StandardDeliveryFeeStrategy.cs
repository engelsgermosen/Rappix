using Microsoft.Extensions.Options;
using Rappix.Pricing.Application.Configuration;
using Rappix.Pricing.Domain.Common;

namespace Rappix.Pricing.Application.Pricing.Delivery;

/// <summary>Tarifa estandar (Food, Grocery, Pharmacy): base + distancia x tarifa por km, desde configuracion.</summary>
internal sealed class StandardDeliveryFeeStrategy(IOptions<PricingOptions> options) : IDeliveryFeeStrategy
{
    public bool Handles(VerticalType vertical) =>
        vertical is VerticalType.Food or VerticalType.Grocery or VerticalType.Pharmacy;

    public decimal Calculate(decimal distanceKm)
    {
        DeliveryRate rate = options.Value.Delivery.Standard;
        return rate.BaseFee + rate.PerKm * Math.Max(0m, distanceKm);
    }
}
