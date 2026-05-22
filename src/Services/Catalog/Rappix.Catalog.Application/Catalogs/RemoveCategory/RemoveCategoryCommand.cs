using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Catalog.Application.Catalogs.RemoveCategory;

/// <summary>Elimina una categoria del catalogo del merchant.</summary>
public sealed record RemoveCategoryCommand(Guid MerchantId, Guid CategoryId) : IRequest<Result>;
