using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Application.Responses;
using Rappix.Merchants.Domain;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Merchants.GetMy;

/// <summary>Devuelve el merchant del owner o NotFound.</summary>
internal sealed class GetMyMerchantQueryHandler(IMerchantRepository merchants)
    : IRequestHandler<GetMyMerchantQuery, Result<MerchantResponse>>
{
    public async Task<Result<MerchantResponse>> Handle(GetMyMerchantQuery query, CancellationToken cancellationToken)
    {
        Merchant? merchant = await merchants.GetByOwnerAsync(query.OwnerUserId, cancellationToken);
        return merchant is null
            ? Result.Failure<MerchantResponse>(MerchantErrors.NotFound)
            : MerchantResponse.From(merchant);
    }
}
