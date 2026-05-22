using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Responses;

namespace Rappix.Catalog.Application.Catalogs.AddCategory;

/// <summary>Agrega una categoria al catalogo del merchant.</summary>
public sealed record AddCategoryCommand(Guid MerchantId, string Name, int SortOrder) : IRequest<Result<CategoryResponse>>;
