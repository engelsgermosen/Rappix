using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Payments;

/// <summary>Publicado cuando el cobro de un pedido fallo. La saga compensa (libera stock, revierte quote) y falla el pedido.</summary>
public sealed record PaymentFailedIntegrationEvent : IntegrationEvent
{
    /// <summary>Pedido cuyo cobro fallo.</summary>
    public required Guid OrderId { get; init; }

    /// <summary>Razon del fallo de cobro.</summary>
    public required string Reason { get; init; }
}
