using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Domain;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Admin.Suspend;

/// <summary>Suspende el merchant con una razon.</summary>
internal sealed class SuspendMerchantCommandHandler(
    IMerchantRepository merchants,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<SuspendMerchantCommand, Result>
{
    public async Task<Result> Handle(SuspendMerchantCommand command, CancellationToken cancellationToken)
    {
        Merchant? merchant = await merchants.GetByIdAsync(new MerchantId(command.MerchantId), cancellationToken);
        if (merchant is null)
        {
            return Result.Failure(MerchantErrors.NotFound);
        }

        Result suspended = merchant.Suspend(command.Reason, clock.UtcNow);
        if (suspended.IsFailure)
        {
            return suspended;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
