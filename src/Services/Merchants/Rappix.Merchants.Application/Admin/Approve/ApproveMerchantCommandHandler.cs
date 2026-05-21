using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Domain;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Admin.Approve;

/// <summary>Aprueba el merchant; el evento de dominio dispara la publicacion del evento de integracion.</summary>
internal sealed class ApproveMerchantCommandHandler(
    IMerchantRepository merchants,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<ApproveMerchantCommand, Result>
{
    public async Task<Result> Handle(ApproveMerchantCommand command, CancellationToken cancellationToken)
    {
        Merchant? merchant = await merchants.GetByIdAsync(new MerchantId(command.MerchantId), cancellationToken);
        if (merchant is null)
        {
            return Result.Failure(MerchantErrors.NotFound);
        }

        Result approved = merchant.Approve(clock.UtcNow);
        if (approved.IsFailure)
        {
            return approved;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
