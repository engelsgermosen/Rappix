using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Responses;

namespace Rappix.Catalog.Application.Items.GetMine;

/// <summary>Devuelve un item del merchant (vista owner) o NotFound.</summary>
public sealed record GetMyItemQuery(Guid MerchantId, Guid ItemId) : IRequest<Result<ItemResponse>>;
