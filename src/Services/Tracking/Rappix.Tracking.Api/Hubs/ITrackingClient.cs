using Rappix.Tracking.Api.Hubs.Payloads;

namespace Rappix.Tracking.Api.Hubs;

/// <summary>
/// Contrato strongly-typed para los pushes del servidor hacia los clientes suscritos al
/// <see cref="TrackingHub"/>. SignalR genera el dispatch dinamico; tener la interfaz tipada elimina
/// errores de strings ("LocationUpdated" / "StatusChanged") y mantiene la API estable.
/// </summary>
public interface ITrackingClient
{
    /// <summary>Notifica al cliente una nueva ubicacion del courier asignado.</summary>
    Task LocationUpdated(LocationUpdatedPayload payload);

    /// <summary>Notifica al cliente un cambio de estado del pedido.</summary>
    Task StatusChanged(StatusChangedPayload payload);
}
