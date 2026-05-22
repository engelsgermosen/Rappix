using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Pricing;

/// <summary>
/// Publicado cuando un cupon se redime efectivamente (al consumir la cotizacion que lo aplico, no al
/// cotizar). Lo consumiran servicios futuros (marketing, analitica) para medir el uso de promociones.
/// </summary>
public sealed record CouponRedeemedIntegrationEvent : IntegrationEvent
{
    /// <summary>Identificador del cupon.</summary>
    public required Guid CouponId { get; init; }

    /// <summary>Codigo del cupon (normalizado en mayusculas).</summary>
    public required string Code { get; init; }

    /// <summary>Cliente que redimio el cupon.</summary>
    public required Guid CustomerUserId { get; init; }

    /// <summary>Cotizacion en la que se redimio.</summary>
    public required Guid QuoteId { get; init; }

    /// <summary>Monto de descuento aplicado por el cupon.</summary>
    public required decimal DiscountAmount { get; init; }
}
