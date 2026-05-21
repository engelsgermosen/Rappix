using MediatR;
using NetTopologySuite.Geometries;
using Rappix.BuildingBlocks.Core.Pagination;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Application.Geo;
using Rappix.Merchants.Application.Responses;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Merchants.SearchNearby;

/// <summary>Construye el punto de busqueda y delega la consulta geoespacial al repositorio.</summary>
internal sealed class SearchNearbyQueryHandler(IMerchantRepository merchants)
    : IRequestHandler<SearchNearbyQuery, Result<IReadOnlyList<NearbyMerchantResponse>>>
{
    public async Task<Result<IReadOnlyList<NearbyMerchantResponse>>> Handle(SearchNearbyQuery query, CancellationToken cancellationToken)
    {
        VerticalType? vertical = query.Vertical is null
            ? null
            : Enum.Parse<VerticalType>(query.Vertical, ignoreCase: true);

        Point point = GeoFactory.CreatePoint(query.Latitude, query.Longitude);
        var paging = new PagedRequest(query.Page, query.PageSize);

        IReadOnlyList<Merchant> found = await merchants.SearchNearbyAsync(point, vertical, paging, cancellationToken);
        IReadOnlyList<NearbyMerchantResponse> result = [.. found.Select(NearbyMerchantResponse.From)];
        return Result.Success(result);
    }
}
