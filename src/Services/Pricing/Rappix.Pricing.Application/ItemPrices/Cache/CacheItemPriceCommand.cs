using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Pricing.Application.ItemPrices.Cache;

/// <summary>Inserta o actualiza el precio base cacheado de un item (lo invoca el consumer de ItemCreated). Idempotente.</summary>
public sealed record CacheItemPriceCommand(
    Guid ItemId,
    Guid MerchantId,
    string Name,
    decimal BasePrice,
    string Currency) : IRequest<Result>;
