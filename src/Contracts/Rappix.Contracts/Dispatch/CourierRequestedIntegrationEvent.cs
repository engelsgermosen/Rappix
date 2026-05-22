using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Dispatch;

/// <summary>
/// Publicado por Orders para solicitar la asignacion de un courier a un pedido pagado. Lo respondera el
/// servicio Dispatch (fase futura); por ahora lo atiende un responder simulado que emite Assigned o Unavailable.
/// </summary>
public sealed record CourierRequestedIntegrationEvent : IntegrationEvent
{
    /// <summary>Pedido que necesita courier (clave de correlacion).</summary>
    public required Guid OrderId { get; init; }

    /// <summary>Merchant (punto de recogida).</summary>
    public required Guid MerchantId { get; init; }

    /// <summary>Latitud de entrega.</summary>
    public required double DeliveryLatitude { get; init; }

    /// <summary>Longitud de entrega.</summary>
    public required double DeliveryLongitude { get; init; }
}
