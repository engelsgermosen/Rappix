using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Orders;

/// <summary>
/// Publicado cuando el pedido se cancela (rechazo del merchant, timeout, sin courier o cancelacion del
/// cliente). Estado terminal de la saga tras las compensaciones (stock liberado, quote revertida).
/// </summary>
public sealed record OrderCancelledIntegrationEvent : IntegrationEvent
{
    /// <summary>Identificador del pedido.</summary>
    public required Guid OrderId { get; init; }

    /// <summary>Razon de la cancelacion.</summary>
    public required string Reason { get; init; }

    /// <summary>Momento de la cancelacion (UTC).</summary>
    public required DateTime CancelledAtUtc { get; init; }
}
