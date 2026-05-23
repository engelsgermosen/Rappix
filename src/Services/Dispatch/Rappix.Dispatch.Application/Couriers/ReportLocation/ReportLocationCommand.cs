using FluentValidation;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Application.Responses;
using Rappix.Dispatch.Domain;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Application.Couriers.ReportLocation;

/// <summary>Reporta la posicion del courier (POST /api/v1/couriers/me/location).</summary>
public sealed record ReportLocationCommand(
    Guid UserId,
    double Latitude,
    double Longitude) : IRequest<Result<CourierResponse>>;

/// <inheritdoc cref="ReportLocationCommand" />
internal sealed class ReportLocationCommandValidator : AbstractValidator<ReportLocationCommand>
{
    public ReportLocationCommandValidator()
    {
        RuleFor(command => command.Latitude).InclusiveBetween(-90, 90);
        RuleFor(command => command.Longitude).InclusiveBetween(-180, 180);
    }
}

/// <inheritdoc cref="ReportLocationCommand" />
internal sealed class ReportLocationCommandHandler(
    ICourierRepository couriers,
    IRedisGeoIndex geo,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<ReportLocationCommand, Result<CourierResponse>>
{
    public async Task<Result<CourierResponse>> Handle(ReportLocationCommand command, CancellationToken cancellationToken)
    {
        CourierProfile? courier = await couriers.GetByIdAsync(CourierId.FromUserId(command.UserId), cancellationToken);
        if (courier is null)
        {
            return Result.Failure<CourierResponse>(CourierErrors.NotFound);
        }

        Result report = courier.ReportLocation(command.Latitude, command.Longitude, clock.UtcNow);
        if (report.IsFailure)
        {
            return Result.Failure<CourierResponse>(report.Error);
        }

        // SaveChanges PRIMERO (DB + outbox de CourierLocationUpdated en la misma transaccion);
        // GEOADD DESPUES — si Redis falla, log y continua, el proximo report reconcilia.
        // Solo Online esta en Redis Geo: si esta Offline o Busy no actualizamos su entrada.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (courier.Status == CourierStatus.Online)
        {
            await geo.AddOrUpdateAsync(courier.Id, command.Latitude, command.Longitude, cancellationToken);
        }

        return CourierResponse.From(courier);
    }
}
