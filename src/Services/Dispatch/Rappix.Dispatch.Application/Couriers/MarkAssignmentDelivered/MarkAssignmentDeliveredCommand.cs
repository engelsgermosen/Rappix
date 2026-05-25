using MassTransit;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Dispatch;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Domain;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Application.Couriers.MarkAssignmentDelivered;

/// <summary>
/// El courier marca su asignacion activa como entregada (POST /api/v1/couriers/me/current-assignment/delivered).
/// Resuelve la asignacion por <c>JWT.sub</c> (ownership implicito); el courier no manda <c>orderId</c>,
/// asi evitamos que pueda marcar pedidos ajenos. Publica <see cref="OrderDeliveredIntegrationEvent"/>
/// — el resto del flujo (release del courier, status "Completed" en Orders, push a Tracking, capture
/// en Payments, notificacion) lo hacen los consumers existentes via bus, sin cambios.
/// </summary>
/// <remarks>
/// Idempotencia: la saga ignora <c>OrderDelivered</c> si el pedido no esta en <c>InProgress</c>
/// (<c>OnUnhandledEvent(context.Ignore())</c>) y el <c>OrderTerminalEventsConsumer</c> en Dispatch
/// es no-op si la asignacion ya esta liberada. Doble-tap del courier produce un segundo evento
/// sin efecto. Por eso este handler no necesita <c>Idempotency-Key</c> server-side.
/// </remarks>
public sealed record MarkAssignmentDeliveredCommand(Guid UserId) : IRequest<Result>;

/// <inheritdoc cref="MarkAssignmentDeliveredCommand" />
internal sealed class MarkAssignmentDeliveredCommandHandler(
    ICourierAssignmentRepository assignments,
    IPublishEndpoint publishEndpoint,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock) : IRequestHandler<MarkAssignmentDeliveredCommand, Result>
{
    public async Task<Result> Handle(MarkAssignmentDeliveredCommand command, CancellationToken cancellationToken)
    {
        CourierAssignment? assignment = await assignments.GetActiveByCourierAsync(
            CourierId.FromUserId(command.UserId), cancellationToken);

        if (assignment is null)
        {
            // 404 — mismo error que cuando no hay asignacion activa. Cubre tanto "nunca tuvo" como
            // "ya entregada" (la fila ya tiene ReleasedAtUtc IS NOT NULL y el repo solo trae activas).
            return Result.Failure(CourierAssignmentErrors.NoActiveAssignment);
        }

        // Publica OrderDelivered (mismo evento que emite el seam temporal de Orders, pero aqui con
        // ownership real). El consumer en Dispatch lo procesa y libera la asignacion (Busy -> Online)
        // — NO liberamos aqui para mantener un solo path de release (terminal events). SaveChanges
        // vacia el outbox EF.
        await publishEndpoint.Publish(new OrderDeliveredIntegrationEvent
        {
            OrderId = assignment.OrderId,
            DeliveredAtUtc = clock.UtcNow,
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
