using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Payments;

/// <summary>Publicado cuando el cobro de un pedido tuvo exito. La saga avanza hacia la asignacion de courier.</summary>
public sealed record PaymentSucceededIntegrationEvent : IntegrationEvent
{
    /// <summary>Pedido cobrado.</summary>
    public required Guid OrderId { get; init; }

    /// <summary>Identificador del pago (del proveedor/servicio de pagos).</summary>
    public required Guid PaymentId { get; init; }

    /// <summary>Monto cobrado.</summary>
    public required decimal Amount { get; init; }
}
