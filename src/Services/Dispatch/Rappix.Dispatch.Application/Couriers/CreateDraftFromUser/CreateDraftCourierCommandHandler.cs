using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Application.Couriers.CreateDraftFromUser;

/// <summary>
/// Crea un CourierProfile en Offline si no existe. Es idempotente en doble capa: explicito via
/// <see cref="ICourierRepository.ExistsAsync"/> + el inbox EF de MassTransit (once-only por messageId).
/// </summary>
internal sealed class CreateDraftCourierCommandHandler(
    ICourierRepository couriers,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<CreateDraftCourierCommand, Result>
{
    public async Task<Result> Handle(CreateDraftCourierCommand command, CancellationToken cancellationToken)
    {
        CourierId courierId = CourierId.FromUserId(command.UserId);

        if (await couriers.ExistsAsync(courierId, cancellationToken))
        {
            return Result.Success();
        }

        string firstName = string.IsNullOrWhiteSpace(command.FirstName) ? "Courier" : command.FirstName.Trim();
        CourierProfile courier = CourierProfile.CreateDraft(courierId, firstName, clock.UtcNow);

        couriers.Add(courier);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
