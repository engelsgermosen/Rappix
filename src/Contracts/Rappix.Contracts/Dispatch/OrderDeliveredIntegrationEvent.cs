using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Dispatch;

/// <summary>
/// Publicado cuando el courier entrega el pedido. Lleva la saga de InProgress a Completed. Lo emite Dispatch
/// al recibir <c>POST /api/v1/couriers/me/current-assignment/delivered</c>.
/// </summary>
public sealed record OrderDeliveredIntegrationEvent : IntegrationEvent
{
    /// <summary>Pedido entregado.</summary>
    public required Guid OrderId { get; init; }

    /// <summary>Momento de la entrega (UTC).</summary>
    public required DateTime DeliveredAtUtc { get; init; }
}
