using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Application.Responses;
using Rappix.Merchants.Domain;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Merchants.GetById;

/// <summary>Devuelve la vista publica solo si el merchant esta Active.</summary>
internal sealed class GetMerchantByIdQueryHandler(IMerchantRepository merchants)
    : IRequestHandler<GetMerchantByIdQuery, Result<PublicMerchantResponse>>
{
    public async Task<Result<PublicMerchantResponse>> Handle(GetMerchantByIdQuery query, CancellationToken cancellationToken)
    {
        Merchant? merchant = await merchants.GetByIdAsync(new MerchantId(query.MerchantId), cancellationToken);
        return merchant is null || !merchant.IsActive
            ? Result.Failure<PublicMerchantResponse>(MerchantErrors.NotFound)
            : PublicMerchantResponse.From(merchant);
    }
}
