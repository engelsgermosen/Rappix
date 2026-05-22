using Rappix.Pricing.Domain.Common;

namespace Rappix.Pricing.Application.Pricing.Delivery;

/// <summary>
/// Estrategia de tarifa de envio por vertical. Pricing NO calcula geo: la distancia llega como input
/// (la calcula el caller con las coordenadas; el calculo geografico es de Merchants/Dispatch).
/// </summary>
public interface IDeliveryFeeStrategy
{
    /// <summary>Indica si esta estrategia aplica al vertical dado.</summary>
    bool Handles(VerticalType vertical);

    /// <summary>Calcula la tarifa de envio para una distancia (km).</summary>
    decimal Calculate(decimal distanceKm);
}
