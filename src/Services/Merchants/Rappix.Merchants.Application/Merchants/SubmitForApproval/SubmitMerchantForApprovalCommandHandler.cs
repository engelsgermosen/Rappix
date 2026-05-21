using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Domain;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Merchants.SubmitForApproval;

/// <summary>Valida completitud y pasa el merchant a Pending.</summary>
internal sealed class SubmitMerchantForApprovalCommandHandler(
    IMerchantRepository merchants,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<SubmitMerchantForApprovalCommand, Result>
{
    public async Task<Result> Handle(SubmitMerchantForApprovalCommand command, CancellationToken cancellationToken)
    {
        Merchant? merchant = await merchants.GetByOwnerAsync(command.OwnerUserId, cancellationToken);
        if (merchant is null)
        {
            return Result.Failure(MerchantErrors.NotFound);
        }

        Result submit = merchant.SubmitForApproval(clock.UtcNow);
        if (submit.IsFailure)
        {
            return submit;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
