using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Application.Responses;
using Rappix.Dispatch.Domain;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Application.Couriers.GoOnline;

/// <summary>Pone al courier Online (POST /api/v1/couriers/me/online).</summary>
public sealed record GoOnlineCommand(Guid UserId) : IRequest<Result<CourierResponse>>;

/// <inheritdoc cref="GoOnlineCommand" />
internal sealed class GoOnlineCommandHandler(
    ICourierRepository couriers,
    IRedisGeoIndex geo,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<GoOnlineCommand, Result<CourierResponse>>
{
    public async Task<Result<CourierResponse>> Handle(GoOnlineCommand command, CancellationToken cancellationToken)
    {
        CourierProfile? courier = await couriers.GetByIdAsync(CourierId.FromUserId(command.UserId), cancellationToken);
        if (courier is null)
        {
            return Result.Failure<CourierResponse>(CourierErrors.NotFound);
        }

        Result transition = courier.GoOnline(clock.UtcNow);
        if (transition.IsFailure)
        {
            return Result.Failure<CourierResponse>(transition.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Redis GEOADD DESPUES de SaveChanges (idea: si el commit falla, no metemos coords stale en Redis;
        // si Redis falla, el log + la rehidratacion al arranque o el proximo report de location reconcilian).
        if (courier.LastLocation is not null)
        {
            await geo.AddOrUpdateAsync(courier.Id, courier.LastLocation.Latitude, courier.LastLocation.Longitude, cancellationToken);
        }

        return CourierResponse.From(courier);
    }
}
