using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Orders;

/// <summary>Publicado cuando el merchant acepta el pedido y la saga avanza hacia el cobro.</summary>
public sealed record OrderAcceptedIntegrationEvent : IntegrationEvent
{
    /// <summary>Identificador del pedido.</summary>
    public required Guid OrderId { get; init; }

    /// <summary>Merchant que acepto.</summary>
    public required Guid MerchantId { get; init; }

    /// <summary>Momento de la aceptacion (UTC).</summary>
    public required DateTime AcceptedAtUtc { get; init; }
}
