using Microsoft.Extensions.Options;
using Rappix.Pricing.Application.Configuration;
using Rappix.Pricing.Domain.Common;

namespace Rappix.Pricing.Application.Pricing.Delivery;

/// <summary>Tarifa de paqueteria (Parcel): base + distancia x tarifa por km, con su propia tarifa (tipicamente mas alta).</summary>
internal sealed class ParcelDeliveryFeeStrategy(IOptions<PricingOptions> options) : IDeliveryFeeStrategy
{
    public bool Handles(VerticalType vertical) => vertical == VerticalType.Parcel;

    public decimal Calculate(decimal distanceKm)
    {
        DeliveryRate rate = options.Value.Delivery.Parcel;
        return rate.BaseFee + rate.PerKm * Math.Max(0m, distanceKm);
    }
}
