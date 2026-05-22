using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Dispatch;

/// <summary>
/// Publicado cuando el courier entrega el pedido. Lleva la saga de InProgress a Completed. Lo emitira el
/// servicio Dispatch (fase futura); hoy lo emite un responder simulado (o el endpoint temporal mark-delivered).
/// </summary>
public sealed record OrderDeliveredIntegrationEvent : IntegrationEvent
{
    /// <summary>Pedido entregado.</summary>
    public required Guid OrderId { get; init; }

    /// <summary>Momento de la entrega (UTC).</summary>
    public required DateTime DeliveredAtUtc { get; init; }
}
