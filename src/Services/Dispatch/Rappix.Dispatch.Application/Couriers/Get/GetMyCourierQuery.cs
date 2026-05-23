using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Application.Responses;
using Rappix.Dispatch.Domain;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Application.Couriers.Get;

/// <summary>Obtiene el perfil del courier autenticado (GET /api/v1/couriers/me).</summary>
public sealed record GetMyCourierQuery(Guid UserId) : IRequest<Result<CourierResponse>>;

/// <inheritdoc cref="GetMyCourierQuery" />
internal sealed class GetMyCourierQueryHandler(ICourierRepository couriers)
    : IRequestHandler<GetMyCourierQuery, Result<CourierResponse>>
{
    public async Task<Result<CourierResponse>> Handle(GetMyCourierQuery query, CancellationToken cancellationToken)
    {
        CourierProfile? courier = await couriers.GetByIdAsync(CourierId.FromUserId(query.UserId), cancellationToken);
        if (courier is null)
        {
            return Result.Failure<CourierResponse>(CourierErrors.NotFound);
        }

        return CourierResponse.From(courier);
    }
}
