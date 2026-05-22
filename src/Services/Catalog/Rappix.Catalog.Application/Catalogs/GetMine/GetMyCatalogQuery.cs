using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Responses;

namespace Rappix.Catalog.Application.Catalogs.GetMine;

/// <summary>Devuelve el catalogo del merchant (con categorias) o NotFound.</summary>
public sealed record GetMyCatalogQuery(Guid MerchantId) : IRequest<Result<CatalogResponse>>;
