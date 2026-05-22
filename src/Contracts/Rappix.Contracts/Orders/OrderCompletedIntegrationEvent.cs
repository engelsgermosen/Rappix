using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Orders;

/// <summary>Publicado cuando el pedido se completa (entregado). Estado terminal feliz de la saga.</summary>
public sealed record OrderCompletedIntegrationEvent : IntegrationEvent
{
    /// <summary>Identificador del pedido.</summary>
    public required Guid OrderId { get; init; }

    /// <summary>Cliente del pedido.</summary>
    public required Guid CustomerUserId { get; init; }

    /// <summary>Merchant del pedido.</summary>
    public required Guid MerchantId { get; init; }

    /// <summary>Momento de la finalizacion (UTC).</summary>
    public required DateTime CompletedAtUtc { get; init; }
}
