using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Domain;

namespace Rappix.Catalog.Application.Catalogs.ResolveMerchant;

/// <summary>Devuelve el MerchantId del usuario propietario, o NotFound si aun no tiene catalogo (merchant no aprobado).</summary>
internal sealed class ResolveMerchantIdQueryHandler(ICatalogRepository catalogs)
    : IRequestHandler<ResolveMerchantIdQuery, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ResolveMerchantIdQuery query, CancellationToken cancellationToken)
    {
        Guid? merchantId = await catalogs.GetMerchantIdByOwnerAsync(query.OwnerUserId, cancellationToken);
        return merchantId is { } id
            ? Result.Success(id)
            : Result.Failure<Guid>(CatalogErrors.NotFound);
    }
}
