using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Application.Responses;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Application.Couriers.GetCurrentAssignment;

/// <summary>
/// Devuelve la asignacion activa del courier (GET /api/v1/couriers/me/current-assignment).
/// El endpoint devuelve 204 si no hay asignacion (Result.Success con null).
/// </summary>
public sealed record GetCurrentAssignmentQuery(Guid UserId) : IRequest<Result<CurrentAssignmentResponse?>>;

/// <inheritdoc cref="GetCurrentAssignmentQuery" />
internal sealed class GetCurrentAssignmentQueryHandler(ICourierAssignmentRepository assignments)
    : IRequestHandler<GetCurrentAssignmentQuery, Result<CurrentAssignmentResponse?>>
{
    public async Task<Result<CurrentAssignmentResponse?>> Handle(GetCurrentAssignmentQuery query, CancellationToken cancellationToken)
    {
        CourierAssignment? assignment = await assignments.GetActiveByCourierAsync(
            CourierId.FromUserId(query.UserId), cancellationToken);

        if (assignment is null)
        {
            return Result.Success<CurrentAssignmentResponse?>(null);
        }

        return Result.Success<CurrentAssignmentResponse?>(new CurrentAssignmentResponse(
            AssignmentId: assignment.Id,
            OrderId: assignment.OrderId,
            AssignedAtUtc: assignment.AssignedAtUtc));
    }
}
