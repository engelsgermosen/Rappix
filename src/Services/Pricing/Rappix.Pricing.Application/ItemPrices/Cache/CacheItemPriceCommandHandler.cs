using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Domain.ItemPrices;

namespace Rappix.Pricing.Application.ItemPrices.Cache;

/// <summary>Upsert idempotente del precio cacheado de un item; tolera reentregas del evento de Catalog.</summary>
internal sealed class CacheItemPriceCommandHandler(
    IItemPriceCacheRepository cache,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<CacheItemPriceCommand, Result>
{
    public async Task<Result> Handle(CacheItemPriceCommand command, CancellationToken cancellationToken)
    {
        ItemPriceCache? existing = await cache.GetByItemIdAsync(command.ItemId, cancellationToken);
        if (existing is null)
        {
            cache.Add(ItemPriceCache.Create(
                command.ItemId, command.MerchantId, command.Name, command.BasePrice, command.Currency, clock.UtcNow));
        }
        else
        {
            existing.Update(command.MerchantId, command.Name, command.BasePrice, command.Currency, clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
