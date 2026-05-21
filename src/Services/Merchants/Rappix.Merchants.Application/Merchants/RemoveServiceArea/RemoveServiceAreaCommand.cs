using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Responses;

namespace Rappix.Merchants.Application.Merchants.RemoveServiceArea;

/// <summary>Quita una zona de cobertura del merchant del owner.</summary>
public sealed record RemoveServiceAreaCommand(Guid OwnerUserId, Guid ServiceAreaId) : IRequest<Result<MerchantResponse>>;
