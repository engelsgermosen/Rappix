using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Application.Responses;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Merchants.GetActivation;

/// <summary>Resuelve el estado de activacion; devuelve un resultado "no encontrado" (sin error) si no existe.</summary>
internal sealed class GetMerchantActivationQueryHandler(IMerchantRepository merchants)
    : IRequestHandler<GetMerchantActivationQuery, Result<MerchantActivationResponse>>
{
    public async Task<Result<MerchantActivationResponse>> Handle(GetMerchantActivationQuery query, CancellationToken cancellationToken)
    {
        Merchant? merchant = await merchants.GetByIdAsync(new MerchantId(query.MerchantId), cancellationToken);
        return merchant is null
            ? MerchantActivationResponse.NotFoundResult
            : MerchantActivationResponse.From(merchant);
    }
}
