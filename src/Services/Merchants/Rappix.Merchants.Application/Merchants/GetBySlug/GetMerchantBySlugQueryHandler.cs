using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Application.Responses;
using Rappix.Merchants.Domain;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Merchants.GetBySlug;

/// <summary>Devuelve la vista publica por slug solo si el merchant esta Active.</summary>
internal sealed class GetMerchantBySlugQueryHandler(IMerchantRepository merchants)
    : IRequestHandler<GetMerchantBySlugQuery, Result<PublicMerchantResponse>>
{
    public async Task<Result<PublicMerchantResponse>> Handle(GetMerchantBySlugQuery query, CancellationToken cancellationToken)
    {
        Result<Slug> slug = Slug.Create(query.Slug);
        if (slug.IsFailure)
        {
            return Result.Failure<PublicMerchantResponse>(MerchantErrors.NotFound);
        }

        Merchant? merchant = await merchants.GetBySlugAsync(slug.Value.Value, cancellationToken);
        return merchant is null || !merchant.IsActive
            ? Result.Failure<PublicMerchantResponse>(MerchantErrors.NotFound)
            : PublicMerchantResponse.From(merchant);
    }
}
