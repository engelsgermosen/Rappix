using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Responses;

namespace Rappix.Merchants.Application.Merchants.GetMy;

/// <summary>Obtiene el merchant del owner autenticado.</summary>
public sealed record GetMyMerchantQuery(Guid OwnerUserId) : IRequest<Result<MerchantResponse>>;
