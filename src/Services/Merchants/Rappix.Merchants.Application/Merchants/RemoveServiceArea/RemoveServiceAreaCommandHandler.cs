using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Application.Responses;
using Rappix.Merchants.Domain;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Merchants.RemoveServiceArea;

/// <summary>Quita la zona indicada del merchant del owner.</summary>
internal sealed class RemoveServiceAreaCommandHandler(
    IMerchantRepository merchants,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<RemoveServiceAreaCommand, Result<MerchantResponse>>
{
    public async Task<Result<MerchantResponse>> Handle(RemoveServiceAreaCommand command, CancellationToken cancellationToken)
    {
        Merchant? merchant = await merchants.GetByOwnerAsync(command.OwnerUserId, cancellationToken);
        if (merchant is null)
        {
            return Result.Failure<MerchantResponse>(MerchantErrors.NotFound);
        }

        Result removed = merchant.RemoveServiceArea(command.ServiceAreaId, clock.UtcNow);
        if (removed.IsFailure)
        {
            return Result.Failure<MerchantResponse>(removed.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MerchantResponse.From(merchant);
    }
}
