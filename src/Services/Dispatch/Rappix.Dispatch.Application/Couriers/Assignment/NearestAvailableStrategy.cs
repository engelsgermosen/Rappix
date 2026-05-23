namespace Rappix.Dispatch.Application.Couriers.Assignment;

/// <summary>
/// Estrategia "primero el mas cercano". Como GEOSEARCH ya devuelve los candidatos ordenados ASC por
/// distancia, simplemente proyecta sus ids. Fase 6 registra esta unica estrategia; el ADR-0007
/// describe como una futura OfferBasedStrategy ocupa el mismo seam.
/// </summary>
internal sealed class NearestAvailableStrategy : ICourierAssignmentStrategy
{
    public Task<CourierAssignmentDecision> ChooseAsync(
        AssignmentContext context,
        IReadOnlyList<CourierCandidate> candidates,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return Task.FromResult(new CourierAssignmentDecision(
                RankedCandidates: [],
                UnavailableReason: "no_candidates"));
        }

        // GEOSEARCH retorna ASC por distancia; respetamos ese orden.
        return Task.FromResult(new CourierAssignmentDecision(
            RankedCandidates: [.. candidates.Select(candidate => candidate.CourierId)],
            UnavailableReason: null));
    }
}
