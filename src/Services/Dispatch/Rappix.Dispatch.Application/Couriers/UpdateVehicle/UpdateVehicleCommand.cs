using FluentValidation;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Application.Responses;
using Rappix.Dispatch.Domain;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Application.Couriers.UpdateVehicle;

/// <summary>Actualiza el vehiculo declarado del courier (PUT /api/v1/couriers/me/vehicle).</summary>
public sealed record UpdateVehicleCommand(
    Guid UserId,
    string VehicleType,
    string? Plate,
    decimal? CapacityKg) : IRequest<Result<CourierResponse>>;

/// <inheritdoc cref="UpdateVehicleCommand" />
internal sealed class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
{
    public UpdateVehicleCommandValidator()
    {
        RuleFor(command => command.VehicleType)
            .Must(value => Enum.TryParse<VehicleType>(value, ignoreCase: true, out _))
            .WithMessage("vehicleType debe ser 'Moto', 'Bici' o 'Carro'.");
    }
}

/// <inheritdoc cref="UpdateVehicleCommand" />
internal sealed class UpdateVehicleCommandHandler(
    ICourierRepository couriers,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<UpdateVehicleCommand, Result<CourierResponse>>
{
    public async Task<Result<CourierResponse>> Handle(UpdateVehicleCommand command, CancellationToken cancellationToken)
    {
        CourierProfile? courier = await couriers.GetByIdAsync(CourierId.FromUserId(command.UserId), cancellationToken);
        if (courier is null)
        {
            return Result.Failure<CourierResponse>(CourierErrors.NotFound);
        }

        VehicleType type = Enum.Parse<VehicleType>(command.VehicleType, ignoreCase: true);
        Result<Vehicle> vehicleResult = Vehicle.Create(type, command.Plate, command.CapacityKg);
        if (vehicleResult.IsFailure)
        {
            return Result.Failure<CourierResponse>(vehicleResult.Error);
        }

        Result set = courier.SetVehicle(vehicleResult.Value, clock.UtcNow);
        if (set.IsFailure)
        {
            return Result.Failure<CourierResponse>(set.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CourierResponse.From(courier);
    }
}
