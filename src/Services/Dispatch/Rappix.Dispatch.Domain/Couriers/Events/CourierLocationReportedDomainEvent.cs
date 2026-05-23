using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Dispatch.Domain.Couriers.Events;

/// <summary>
/// Reporte de posicion del courier. El handler traduce a CourierLocationUpdatedIntegrationEvent
/// (Fase 7 Tracking hace streaming al cliente).
/// </summary>
public sealed record CourierLocationReportedDomainEvent(
    CourierId CourierId,
    double Latitude,
    double Longitude,
    DateTime ReportedAtUtc) : DomainEvent;
