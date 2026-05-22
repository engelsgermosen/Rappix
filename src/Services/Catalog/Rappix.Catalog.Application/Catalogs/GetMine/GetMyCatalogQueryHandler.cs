using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Application.Responses;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Catalogs;

namespace Rappix.Catalog.Application.Catalogs.GetMine;

/// <summary>Devuelve el catalogo del merchant o NotFound.</summary>
internal sealed class GetMyCatalogQueryHandler(ICatalogRepository catalogs)
    : IRequestHandler<GetMyCatalogQuery, Result<CatalogResponse>>
{
    public async Task<Result<CatalogResponse>> Handle(GetMyCatalogQuery query, CancellationToken cancellationToken)
    {
        MerchantCatalog? catalog = await catalogs.GetByMerchantAsync(query.MerchantId, cancellationToken);
        return catalog is null
            ? Result.Failure<CatalogResponse>(CatalogErrors.NotFound)
            : CatalogResponse.From(catalog);
    }
}
