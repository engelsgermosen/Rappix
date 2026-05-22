using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Responses;

namespace Rappix.Catalog.Application.Public.GetItem;

/// <summary>Devuelve la vista publica de un item si esta disponible y su catalogo habilitado.</summary>
public sealed record GetPublicItemQuery(Guid ItemId) : IRequest<Result<PublicItemResponse>>;
