using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Dispatch;

/// <summary>
/// Publicado por Dispatch (Fase 6) cuando un courier cambia de estado de disponibilidad
/// (Offline/Online/Busy). Lo consumira Fase 7 (Tracking) o un futuro dashboard. Dispatch no se
/// consume a si mismo — solo lo emite.
/// </summary>
public sealed record CourierAvailabilityChangedIntegrationEvent : IntegrationEvent
{
    /// <summary>Courier que cambio.</summary>
    public required Guid CourierId { get; init; }

    /// <summary>Nuevo estado: "Offline" | "Online" | "Busy".</summary>
    public required string Status { get; init; }

    /// <summary>Momento del cambio (UTC).</summary>
    public required DateTime ChangedAtUtc { get; init; }
}
