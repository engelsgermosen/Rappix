using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Pricing;

/// <summary>
/// Publicado cuando el servicio Pricing crea una cotizacion (Quote) para un cliente. Lo consumiran
/// servicios futuros (analitica, Orders) que necesiten conocer las cotizaciones emitidas.
/// </summary>
public sealed record QuoteCreatedIntegrationEvent : IntegrationEvent
{
    /// <summary>Identificador de la cotizacion.</summary>
    public required Guid QuoteId { get; init; }

    /// <summary>Cliente para quien se cotizo.</summary>
    public required Guid CustomerUserId { get; init; }

    /// <summary>Merchant del pedido cotizado.</summary>
    public required Guid MerchantId { get; init; }

    /// <summary>Total a cobrar (incluye fees, impuestos y propina).</summary>
    public required decimal Total { get; init; }

    /// <summary>Moneda ISO 4217 del total (p. ej. DOP).</summary>
    public required string Currency { get; init; }

    /// <summary>Momento de expiracion de la cotizacion (UTC).</summary>
    public required DateTime ExpiresAtUtc { get; init; }
}
