using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Dispatch;

/// <summary>
/// Publicado por Dispatch (Fase 6) cada vez que un courier reporta su ubicacion (POST /me/location).
/// Lo consumira Fase 7 (Tracking) para hacer streaming en tiempo real al cliente. Dispatch no se lo
/// consume a si mismo — solo lo emite.
/// </summary>
public sealed record CourierLocationUpdatedIntegrationEvent : IntegrationEvent
{
    /// <summary>Courier que reporto.</summary>
    public required Guid CourierId { get; init; }

    /// <summary>Latitud reportada.</summary>
    public required double Latitude { get; init; }

    /// <summary>Longitud reportada.</summary>
    public required double Longitude { get; init; }

    /// <summary>Momento del reporte (UTC).</summary>
    public required DateTime ReportedAtUtc { get; init; }
}
