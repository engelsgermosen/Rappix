using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Application.Responses;
using Rappix.Dispatch.Domain;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Application.Couriers.GoOffline;

/// <summary>Pone al courier Offline (POST /api/v1/couriers/me/offline).</summary>
public sealed record GoOfflineCommand(Guid UserId) : IRequest<Result<CourierResponse>>;

/// <inheritdoc cref="GoOfflineCommand" />
internal sealed class GoOfflineCommandHandler(
    ICourierRepository couriers,
    IRedisGeoIndex geo,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<GoOfflineCommand, Result<CourierResponse>>
{
    public async Task<Result<CourierResponse>> Handle(GoOfflineCommand command, CancellationToken cancellationToken)
    {
        CourierProfile? courier = await couriers.GetByIdAsync(CourierId.FromUserId(command.UserId), cancellationToken);
        if (courier is null)
        {
            return Result.Failure<CourierResponse>(CourierErrors.NotFound);
        }

        Result transition = courier.GoOffline(clock.UtcNow);
        if (transition.IsFailure)
        {
            return Result.Failure<CourierResponse>(transition.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Quitar de Redis Geo DESPUES de SaveChanges: el courier queda fuera del matching.
        await geo.RemoveAsync(courier.Id, cancellationToken);

        return CourierResponse.From(courier);
    }
}
