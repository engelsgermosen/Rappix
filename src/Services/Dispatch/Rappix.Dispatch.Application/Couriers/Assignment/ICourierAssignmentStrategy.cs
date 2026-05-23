using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Application.Couriers.Assignment;

/// <summary>
/// Decide a que courier(s) intentar asignar un pedido. Devuelve un RANKING, no una eleccion unica:
/// el orchestrator itera el ranking intentando el claim atomico contra cada candidato hasta que uno
/// gana la carrera (rows == 1). Esta forma soporta tanto NearestAvailable (ranking = orden de
/// distancia ASC) como una futura OfferBased (ranking = prioridad de oferta).
/// </summary>
public interface ICourierAssignmentStrategy
{
    /// <summary>Calcula el ranking de candidatos a partir del contexto del pedido y los disponibles.</summary>
    Task<CourierAssignmentDecision> ChooseAsync(
        AssignmentContext context,
        IReadOnlyList<CourierCandidate> candidates,
        CancellationToken cancellationToken);
}

/// <summary>Contexto del pedido para el matching geo + filtros (vehicle, capacity, etc.).</summary>
public sealed record AssignmentContext(
    Guid OrderId,
    Guid MerchantId,
    double PickupLatitude,
    double PickupLongitude,
    double DeliveryLatitude,
    double DeliveryLongitude);

/// <summary>
/// Candidato traido por GEOSEARCH + cross-check de BD (solo couriers Online con LastLocation
/// conocida y status Online en BD).
/// </summary>
public sealed record CourierCandidate(
    CourierId CourierId,
    double Latitude,
    double Longitude,
    double DistanceMeters,
    VehicleType Vehicle);

/// <summary>
/// Decision del strategy: lista ordenada de candidatos a probar y un opcional UnavailableReason
/// si la lista esta vacia (p. ej. "no_candidates").
/// </summary>
public sealed record CourierAssignmentDecision(
    IReadOnlyList<CourierId> RankedCandidates,
    string? UnavailableReason);
