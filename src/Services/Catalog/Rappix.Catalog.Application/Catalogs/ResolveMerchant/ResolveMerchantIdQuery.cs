using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Catalog.Application.Catalogs.ResolveMerchant;

/// <summary>
/// Resuelve el MerchantId del usuario propietario autenticado (sujeto JWT) usando el OwnerUserId
/// cacheado en el catalogo. Lo usa la capa API para mapear el usuario a su merchant antes de operar.
/// </summary>
public sealed record ResolveMerchantIdQuery(Guid OwnerUserId) : IRequest<Result<Guid>>;
