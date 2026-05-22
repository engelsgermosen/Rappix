using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Catalog.Application.Catalogs.Create;

/// <summary>Crea el catalogo de un merchant aprobado. Idempotente. Invocado por el consumer de MerchantApproved.</summary>
public sealed record CreateCatalogCommand(Guid MerchantId, Guid OwnerUserId, string VerticalType) : IRequest<Result>;
