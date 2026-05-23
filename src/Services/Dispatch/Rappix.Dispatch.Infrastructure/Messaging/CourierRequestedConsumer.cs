using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Dispatch;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Application.Configuration;
using Rappix.Dispatch.Application.Couriers.Assignment;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Infrastructure.Messaging;

/// <summary>
/// Atiende <see cref="CourierRequestedIntegrationEvent"/> publicado por la saga de Orders al pasar a
/// AwaitingCourier. Resuelve un courier via GEOSEARCH + claim atomico (predicado WHERE Status='Online')
/// y publica <see cref="CourierAssignedIntegrationEvent"/> o <see cref="CourierUnavailableIntegrationEvent"/>.
/// </summary>
/// <remarks>
/// Flujo:
/// <list type="number">
/// <item><b>Idempotency guard</b>: si ya existe asignacion activa para el OrderId, no-op (la
/// reentrega del mensaje encontraria al pedido ya asignado y el publish original esta en outbox).</item>
/// <item><b>GEOSEARCH</b> alrededor del pickup (ya viene en el payload — Orders propaga merchant pickup).</item>
/// <item><b>Filtro Online</b>: para los ids del Redis Geo, cross-check en BD (descarta couriers que
/// pasaron a Busy/Offline desde el ultimo GEOADD si Redis quedo desincronizado).</item>
/// <item><b>Strategy.ChooseAsync</b>: devuelve un RANKING de candidatos.</item>
/// <item><b>Claim atomico</b> iterando: <c>ICourierRepository.TryClaimAsync</c> (UPDATE ... WHERE
/// Status='Online' RETURNING). El predicado ES el concurrency token: dos consumers compitiendo por
/// el mismo courier, solo uno actualiza la fila; el otro recibe false y prueba con el siguiente.</item>
/// <item><b>Persistir asignacion</b>: <c>CourierAssignment.Create</c> + Add. Los 2 unique partial
/// indexes en BD (released_at_utc IS NULL sobre courier_id y order_id) son la red de seguridad ante
/// carreras imposibles desde codigo.</item>
/// <item><b>ZREM</b> de Redis Geo (el courier ya esta Busy, no aparece en futuras busquedas).</item>
/// <item><b>Publish CourierAssigned</b> o <b>CourierUnavailable</b>. AddConfigureEndpointsCallback
/// con outbox EF garantiza que el publish entra en la misma transaccion del SaveChanges.</item>
/// </list>
/// </remarks>
internal sealed partial class CourierRequestedConsumer(
    ICourierRepository couriers,
    ICourierAssignmentRepository assignments,
    IRedisGeoIndex geo,
    ICourierAssignmentStrategy strategy,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IOptions<DispatchOptions> options,
    ILogger<CourierRequestedConsumer> logger)
    : IConsumer<CourierRequestedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<CourierRequestedIntegrationEvent> context)
    {
        CourierRequestedIntegrationEvent message = context.Message;
        CancellationToken cancellationToken = context.CancellationToken;

        // 1) Idempotency guard: si ya hay asignacion activa para el pedido, no-op.
        CourierAssignment? existing = await assignments.GetActiveByOrderAsync(message.OrderId, cancellationToken);
        if (existing is not null)
        {
            LogAlreadyAssigned(logger, message.OrderId, existing.CourierId.Value);
            return;
        }

        DispatchOptions.MatchingOptions matching = options.Value.Matching;

        // 2) GEOSEARCH alrededor del pickup.
        IReadOnlyList<NearbyCourier> nearby = await geo.SearchNearbyAsync(
            message.PickupLatitude,
            message.PickupLongitude,
            matching.RadiusMeters,
            matching.CandidateLimit,
            cancellationToken);

        // 3) Cross-check Online en BD (filtro de seguridad si Redis quedo desincronizado).
        CourierCandidate[] candidates = [];
        if (nearby.Count > 0)
        {
            IReadOnlyList<CourierProfile> profiles = await couriers.GetOnlineByIdsAsync(
                [.. nearby.Select(near => near.CourierId)], cancellationToken);

            // Mantener el orden del GEOSEARCH (ASC por distancia) restringiendo a los Online en BD.
            var onlineSet = profiles.ToDictionary(profile => profile.Id);
            candidates = [.. nearby
                .Where(near => onlineSet.ContainsKey(near.CourierId))
                .Select(near => new CourierCandidate(
                    CourierId: near.CourierId,
                    Latitude: near.Latitude,
                    Longitude: near.Longitude,
                    DistanceMeters: near.DistanceMeters,
                    Vehicle: onlineSet[near.CourierId].Vehicle?.Type ?? VehicleType.Moto))];
        }

        // 4) Strategy.ChooseAsync — ranking.
        var assignmentContext = new AssignmentContext(
            OrderId: message.OrderId,
            MerchantId: message.MerchantId,
            PickupLatitude: message.PickupLatitude,
            PickupLongitude: message.PickupLongitude,
            DeliveryLatitude: message.DeliveryLatitude,
            DeliveryLongitude: message.DeliveryLongitude);
        CourierAssignmentDecision decision = await strategy.ChooseAsync(assignmentContext, candidates, cancellationToken);

        // 5) Claim atomico iterando el ranking.
        DateTime now = clock.UtcNow;
        CourierId? chosen = null;
        foreach (CourierId candidateId in decision.RankedCandidates)
        {
            if (await couriers.TryClaimAsync(candidateId, now, cancellationToken))
            {
                chosen = candidateId;
                break;
            }
        }

        if (chosen is null)
        {
            string reason = decision.UnavailableReason ?? "no_candidates_won_race";
            LogUnavailable(logger, message.OrderId, reason);
            await context.Publish(
                new CourierUnavailableIntegrationEvent { OrderId = message.OrderId, Reason = reason },
                cancellationToken);
            return;
        }

        // 6) Persistir asignacion (la unique partial index es la red de seguridad).
        CourierAssignment assignment = CourierAssignment.Create(chosen.Value, message.OrderId, now);
        assignments.Add(assignment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 7) ZREM del geo set: el courier Busy no debe aparecer en proximas busquedas.
        await geo.RemoveAsync(chosen.Value, cancellationToken);

        // 8) Publish CourierAssigned. La transaccion del consume (AddConfigureEndpointsCallback +
        //    UseEntityFrameworkOutbox) garantiza que este publish entra en outbox dentro del
        //    SaveChanges del paso 6 — leccion critica de Orders.
        LogAssigned(logger, message.OrderId, chosen.Value.Value);
        await context.Publish(
            new CourierAssignedIntegrationEvent { OrderId = message.OrderId, CourierId = chosen.Value.Value },
            cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pedido {OrderId} ya tenia asignacion activa al courier {CourierId}: reentrega ignorada.")]
    private static partial void LogAlreadyAssigned(ILogger logger, Guid orderId, Guid courierId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Courier asignado al pedido {OrderId}: {CourierId}.")]
    private static partial void LogAssigned(ILogger logger, Guid orderId, Guid courierId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sin courier para el pedido {OrderId}: {Reason}.")]
    private static partial void LogUnavailable(ILogger logger, Guid orderId, string reason);
}
