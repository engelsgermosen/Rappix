using Rappix.BuildingBlocks.Messaging.Integration;
using Rappix.Contracts.Orders;

namespace Rappix.Contracts.Dispatch;

/// <summary>
/// Publicado por Orders para solicitar la asignacion de un courier a un pedido pagado. Lo responde
/// el servicio Dispatch (Fase 6): hace GEOSEARCH de couriers cercanos al pickup y publica
/// CourierAssigned o CourierUnavailable. Lleva pickup + delivery coords para que Dispatch no
/// necesite llamar a Merchants por gRPC.
/// </summary>
/// <remarks>
/// Fase 13.6: ampliado con los campos del snapshot del pedido (CustomerUserId, MerchantName, delivery
/// street/reference, total/currency, lines) que Dispatch persiste en CourierAssignment.AssignmentSnapshot
/// para responder GET /api/v1/couriers/me/current-assignment con direcciones + items sin llamadas
/// cross-service en caliente. El snapshot es atomico con el publish del evento: misma transaccion EF
/// outbox que actualiza OrderState.
/// </remarks>
public sealed record CourierRequestedIntegrationEvent : IntegrationEvent
{
    /// <summary>Pedido que necesita courier (clave de correlacion).</summary>
    public required Guid OrderId { get; init; }

    /// <summary>Merchant (referencia del comercio).</summary>
    public required Guid MerchantId { get; init; }

    /// <summary>Latitud del pickup (ubicacion fisica del comercio).</summary>
    public required double PickupLatitude { get; init; }

    /// <summary>Longitud del pickup (ubicacion fisica del comercio).</summary>
    public required double PickupLongitude { get; init; }

    /// <summary>Latitud de entrega.</summary>
    public required double DeliveryLatitude { get; init; }

    /// <summary>Longitud de entrega.</summary>
    public required double DeliveryLongitude { get; init; }

    /// <summary>Cliente del pedido (Fase 13.6 — viaja al snapshot para que el courier vea "Cliente #xxxxxxxx").</summary>
    public required Guid CustomerUserId { get; init; }

    /// <summary>Nombre del comercio congelado al momento de crear el pedido (Fase 13.6).</summary>
    public required string MerchantName { get; init; }

    /// <summary>Direccion de entrega (texto legible) — Fase 13.6.</summary>
    public required string DeliveryStreet { get; init; }

    /// <summary>Referencia opcional de la direccion (apto, piso, punto de referencia) — Fase 13.6.</summary>
    public string? DeliveryReference { get; init; }

    /// <summary>Total a cobrar del pedido (snapshot) — Fase 13.6.</summary>
    public required decimal OrderTotal { get; init; }

    /// <summary>Moneda ISO 4217 del total — Fase 13.6.</summary>
    public required string OrderCurrency { get; init; }

    /// <summary>Snapshot de las lineas del pedido (nombre + cantidad) — Fase 13.6.</summary>
    public required IReadOnlyList<OrderLineSnapshot> Lines { get; init; }
}
