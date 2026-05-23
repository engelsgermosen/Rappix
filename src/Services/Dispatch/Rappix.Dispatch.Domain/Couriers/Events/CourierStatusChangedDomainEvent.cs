using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Dispatch.Domain.Couriers.Events;

/// <summary>
/// Cambio de estado de disponibilidad del courier. El handler en Application lo traduce a
/// CourierAvailabilityChangedIntegrationEvent (Fase 7 Tracking lo consumira).
/// </summary>
public sealed record CourierStatusChangedDomainEvent(
    CourierId CourierId,
    CourierStatus FromStatus,
    CourierStatus ToStatus,
    DateTime ChangedAtUtc) : DomainEvent;
