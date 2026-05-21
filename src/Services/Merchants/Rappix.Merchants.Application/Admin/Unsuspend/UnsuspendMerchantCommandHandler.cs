using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Domain;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Admin.Unsuspend;

/// <summary>Reactiva el merchant suspendido.</summary>
internal sealed class UnsuspendMerchantCommandHandler(
    IMerchantRepository merchants,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<UnsuspendMerchantCommand, Result>
{
    public async Task<Result> Handle(UnsuspendMerchantCommand command, CancellationToken cancellationToken)
    {
        Merchant? merchant = await merchants.GetByIdAsync(new MerchantId(command.MerchantId), cancellationToken);
        if (merchant is null)
        {
            return Result.Failure(MerchantErrors.NotFound);
        }

        Result reactivated = merchant.Unsuspend(clock.UtcNow);
        if (reactivated.IsFailure)
        {
            return reactivated;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
