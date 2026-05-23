namespace Rappix.Tracking.Domain.OrderTrackings;

/// <summary>
/// Estados visibles al cliente en la timeline del pedido. La granularidad esta acotada por los
/// IntegrationEvents que Orders y Dispatch publican hoy: <see cref="Placed"/> al recibir
/// OrderSubmitted; <see cref="MerchantAccepted"/> al recibir OrderAccepted; <see cref="CourierAssigned"/>
/// al recibir CourierAssigned (Dispatch); <see cref="Delivered"/> al recibir OrderDelivered/OrderCompleted;
/// <see cref="Cancelled"/>/<see cref="Failed"/> al recibir los terminales correspondientes.
/// Estados intermedios mas finos (Preparing/PickedUp/InTransit) son un follow-up para cuando
/// la saga publique OrderStatusChangedIntegrationEvent (ADR-0008).
/// </summary>
public enum TrackingStatus
{
    /// <summary>Pedido creado, esperando aceptacion del merchant.</summary>
    Placed = 0,

    /// <summary>Merchant acepto; saga avanzando hacia pago y asignacion del courier.</summary>
    MerchantAccepted = 1,

    /// <summary>Courier asignado; ubicacion en tiempo real disponible.</summary>
    CourierAssigned = 2,

    /// <summary>Pedido entregado (ancla de negocio = OrderDelivered de Dispatch).</summary>
    Delivered = 3,

    /// <summary>Pedido cancelado tras compensacion (con razon).</summary>
    Cancelled = 4,

    /// <summary>Pedido fallido tras compensacion (con razon).</summary>
    Failed = 5,
}
