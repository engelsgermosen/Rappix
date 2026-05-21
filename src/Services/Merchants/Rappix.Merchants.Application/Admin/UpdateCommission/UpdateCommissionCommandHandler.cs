using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Application.Responses;
using Rappix.Merchants.Domain;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Admin.UpdateCommission;

/// <summary>Actualiza la comision del merchant.</summary>
internal sealed class UpdateCommissionCommandHandler(
    IMerchantRepository merchants,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<UpdateCommissionCommand, Result<MerchantResponse>>
{
    public async Task<Result<MerchantResponse>> Handle(UpdateCommissionCommand command, CancellationToken cancellationToken)
    {
        Merchant? merchant = await merchants.GetByIdAsync(new MerchantId(command.MerchantId), cancellationToken);
        if (merchant is null)
        {
            return Result.Failure<MerchantResponse>(MerchantErrors.NotFound);
        }

        Result<CommissionPercentage> commission = CommissionPercentage.Create(command.CommissionPercentage);
        if (commission.IsFailure)
        {
            return Result.Failure<MerchantResponse>(commission.Error);
        }

        merchant.UpdateCommission(commission.Value, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MerchantResponse.From(merchant);
    }
}
