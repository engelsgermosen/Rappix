using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Domain;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Admin.Reject;

/// <summary>Rechaza el merchant con una razon.</summary>
internal sealed class RejectMerchantCommandHandler(
    IMerchantRepository merchants,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<RejectMerchantCommand, Result>
{
    public async Task<Result> Handle(RejectMerchantCommand command, CancellationToken cancellationToken)
    {
        Merchant? merchant = await merchants.GetByIdAsync(new MerchantId(command.MerchantId), cancellationToken);
        if (merchant is null)
        {
            return Result.Failure(MerchantErrors.NotFound);
        }

        Result rejected = merchant.Reject(command.Reason, clock.UtcNow);
        if (rejected.IsFailure)
        {
            return rejected;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
