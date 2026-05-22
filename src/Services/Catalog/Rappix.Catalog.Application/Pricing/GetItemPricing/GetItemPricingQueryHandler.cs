using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Application.Responses;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Catalogs;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Pricing.GetItemPricing;

/// <summary>
/// Resuelve precio y comprabilidad de un item. Consulta el estado autoritativo del merchant via gRPC
/// (IMerchantValidationClient); si el servicio Merchants no responde, hace fallback al gating local
/// cacheado (Catalog.IsEnabled).
/// </summary>
internal sealed class GetItemPricingQueryHandler(
    IItemRepository items,
    IStockRepository stocks,
    ICatalogRepository catalogs,
    IMerchantValidationClient merchantClient)
    : IRequestHandler<GetItemPricingQuery, Result<ItemPricingResponse>>
{
    public async Task<Result<ItemPricingResponse>> Handle(GetItemPricingQuery query, CancellationToken cancellationToken)
    {
        Item? item = await items.GetByIdAsync(new ItemId(query.ItemId), cancellationToken);
        if (item is null || item.IsDeleted)
        {
            return Result.Failure<ItemPricingResponse>(ItemErrors.NotFound);
        }

        MerchantCatalog? catalog = await catalogs.GetByMerchantAsync(item.MerchantId, cancellationToken);
        MerchantValidation validation = await merchantClient.ValidateAsync(item.MerchantId, cancellationToken);
        bool merchantActive = validation.ServiceAvailable
            ? validation.IsActive
            : catalog?.IsEnabled ?? false;

        int? stockQuantity = null;
        bool inStock = true;
        if (item.TracksInventory)
        {
            StockLevel? stock = await stocks.GetByItemIdAsync(item.Id, cancellationToken);
            // stock_quantity informa el fisico en existencia; la comprabilidad usa el disponible
            // (fisico menos holds activos) para no vender unidades ya apartadas por otra saga.
            stockQuantity = stock?.Quantity ?? 0;
            inStock = (stock?.Available ?? 0) > 0;
        }

        bool purchasable = merchantActive && item.IsAvailable && inStock;

        return new ItemPricingResponse(
            item.Id.Value,
            item.MerchantId,
            item.Name,
            item.Price.Amount,
            item.Price.Currency,
            item.IsAvailable,
            item.TracksInventory,
            stockQuantity,
            purchasable);
    }
}
