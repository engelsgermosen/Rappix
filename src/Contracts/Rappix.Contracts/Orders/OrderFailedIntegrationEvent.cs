using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Orders;

/// <summary>
/// Publicado cuando el pedido falla (fallo de pago, de reserva de stock o de consumo de quote). Estado
/// terminal de la saga tras las compensaciones correspondientes.
/// </summary>
public sealed record OrderFailedIntegrationEvent : IntegrationEvent
{
    /// <summary>Identificador del pedido.</summary>
    public required Guid OrderId { get; init; }

    /// <summary>Razon del fallo.</summary>
    public required string Reason { get; init; }

    /// <summary>Momento del fallo (UTC).</summary>
    public required DateTime FailedAtUtc { get; init; }
}
