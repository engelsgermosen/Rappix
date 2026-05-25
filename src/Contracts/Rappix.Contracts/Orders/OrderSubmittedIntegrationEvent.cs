using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Orders;

/// <summary>
/// Publicado cuando un cliente envia un pedido (tras crearse el agregado Order). Arranca la saga de Orders
/// (se correlaciona por <see cref="OrderId"/>) y notifica a interesados externos. Lleva el snapshot minimo
/// que la saga necesita para orquestar (consumir quote, cobrar pago, pedir courier) sin releer el pedido.
/// </summary>
public sealed record OrderSubmittedIntegrationEvent : IntegrationEvent
{
    /// <summary>Identificador del pedido (clave de correlacion de la saga).</summary>
    public required Guid OrderId { get; init; }

    /// <summary>Cliente que realizo el pedido.</summary>
    public required Guid CustomerUserId { get; init; }

    /// <summary>Merchant del pedido.</summary>
    public required Guid MerchantId { get; init; }

    /// <summary>Cotizacion congelada que respalda el pedido (la saga la consume).</summary>
    public required Guid QuoteId { get; init; }

    /// <summary>Total a cobrar (del quote congelado).</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>Moneda ISO 4217.</summary>
    public required string Currency { get; init; }

    /// <summary>Direccion de entrega (texto legible).</summary>
    public required string DeliveryAddress { get; init; }

    /// <summary>Referencia opcional de la direccion (apto, piso, punto de referencia). Fase 13.6.</summary>
    public string? DeliveryReference { get; init; }

    /// <summary>Latitud de entrega.</summary>
    public required double DeliveryLatitude { get; init; }

    /// <summary>Longitud de entrega.</summary>
    public required double DeliveryLongitude { get; init; }

    /// <summary>Latitud del pickup (ubicacion fisica del comercio). La saga la propaga a CourierRequested.</summary>
    public required double PickupLatitude { get; init; }

    /// <summary>Longitud del pickup.</summary>
    public required double PickupLongitude { get; init; }

    /// <summary>
    /// Nombre del comercio congelado al momento de crear el pedido (Fase 13.6). La saga lo guarda en
    /// OrderState y lo propaga a CourierRequested para que Dispatch lo persista en el snapshot del
    /// CourierAssignment ("Recoger en {MerchantName}").
    /// </summary>
    public required string MerchantName { get; init; }

    /// <summary>
    /// Snapshot inmutable de las lineas del pedido (Fase 13.6). Solo nombre + cantidad — la saga lo
    /// serializa a JSON en OrderState.LinesJson y lo propaga a CourierRequested. Lista vacia es valida
    /// para eventos pre-13.6 que se procesen post-deploy (el aggregate Order garantiza minimo 1 linea
    /// en pedidos nuevos).
    /// </summary>
    public required IReadOnlyList<OrderLineSnapshot> Lines { get; init; }
}
