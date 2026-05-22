using MediatR;
using Rappix.BuildingBlocks.Core.Pagination;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Responses;

namespace Rappix.Catalog.Application.Public.Search;

/// <summary>Busqueda publica full-text de items disponibles (catalogo habilitado), opcional por merchant/categoria.</summary>
public sealed record SearchItemsQuery(string? Text, Guid? MerchantId, Guid? CategoryId, int Page, int PageSize)
    : IRequest<Result<PagedResult<PublicItemResponse>>>;
