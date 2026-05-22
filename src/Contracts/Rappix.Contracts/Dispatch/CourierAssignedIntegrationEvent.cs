using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Dispatch;

/// <summary>Publicado cuando se asigno un courier al pedido. La saga confirma el stock y pasa el pedido a en curso.</summary>
public sealed record CourierAssignedIntegrationEvent : IntegrationEvent
{
    /// <summary>Pedido con courier asignado.</summary>
    public required Guid OrderId { get; init; }

    /// <summary>Courier asignado.</summary>
    public required Guid CourierId { get; init; }
}
