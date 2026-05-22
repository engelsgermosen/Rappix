using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Storage;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Domain;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Merchants.GetLogoUrl;

/// <summary>Genera la URL firmada del logo (1h) si el merchant esta Active y tiene logo.</summary>
internal sealed class GetLogoUrlQueryHandler(IMerchantRepository merchants, IObjectStorage storage)
    : IRequestHandler<GetLogoUrlQuery, Result<string>>
{
    private static readonly TimeSpan LogoUrlLifetime = TimeSpan.FromHours(1);

    public async Task<Result<string>> Handle(GetLogoUrlQuery query, CancellationToken cancellationToken)
    {
        Merchant? merchant = await merchants.GetByIdAsync(new MerchantId(query.MerchantId), cancellationToken);
        if (merchant is null || !merchant.IsActive || merchant.LogoObjectKey is null)
        {
            return Result.Failure<string>(MerchantErrors.NotFound);
        }

        string url = await storage.GetPresignedUrlAsync(merchant.LogoObjectKey, LogoUrlLifetime, cancellationToken);
        return Result.Success(url);
    }
}
