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

    /// <summary>Latitud de entrega.</summary>
    public required double DeliveryLatitude { get; init; }

    /// <summary>Longitud de entrega.</summary>
    public required double DeliveryLongitude { get; init; }

    /// <summary>Latitud del pickup (ubicacion fisica del comercio). La saga la propaga a CourierRequested.</summary>
    public required double PickupLatitude { get; init; }

    /// <summary>Longitud del pickup.</summary>
    public required double PickupLongitude { get; init; }
}
