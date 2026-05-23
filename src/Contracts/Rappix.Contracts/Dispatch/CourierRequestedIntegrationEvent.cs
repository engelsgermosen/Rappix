using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Dispatch;

/// <summary>
/// Publicado por Orders para solicitar la asignacion de un courier a un pedido pagado. Lo responde
/// el servicio Dispatch (Fase 6): hace GEOSEARCH de couriers cercanos al pickup y publica
/// CourierAssigned o CourierUnavailable. Lleva pickup + delivery coords para que Dispatch no
/// necesite llamar a Merchants por gRPC.
/// </summary>
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
}
