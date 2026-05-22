using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Dispatch;

/// <summary>
/// Publicado cuando no hay courier disponible para el pedido. La saga compensa (reembolso, libera stock,
/// revierte quote) y cancela el pedido.
/// </summary>
public sealed record CourierUnavailableIntegrationEvent : IntegrationEvent
{
    /// <summary>Pedido sin courier.</summary>
    public required Guid OrderId { get; init; }

    /// <summary>Razon (sin couriers en zona, fuera de horario, etc.).</summary>
    public required string Reason { get; init; }
}
