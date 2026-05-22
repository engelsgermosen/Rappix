using System.Globalization;
using Grpc.Core;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Pricing.GetItemPricing;
using PricingResult = Rappix.Catalog.Application.Responses.ItemPricingResponse;

namespace Rappix.Catalog.Api.Grpc;

/// <summary>Implementacion gRPC del servicio interno de pricing/validacion de items. Delega en MediatR.</summary>
internal sealed class CatalogValidationServiceImpl(ISender sender)
    : CatalogValidationService.CatalogValidationServiceBase
{
    public override async Task<ItemPricingResponse> GetItemPricing(ItemPricingRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ItemId, out Guid itemId))
        {
            return new ItemPricingResponse { Found = false };
        }

        Result<PricingResult> result = await sender.Send(new GetItemPricingQuery(itemId), context.CancellationToken);
        if (result.IsFailure)
        {
            return new ItemPricingResponse { Found = false };
        }

        PricingResult pricing = result.Value;
        return new ItemPricingResponse
        {
            Found = true,
            ItemId = pricing.ItemId.ToString(),
            MerchantId = pricing.MerchantId.ToString(),
            Name = pricing.Name,
            PriceAmount = pricing.PriceAmount.ToString(CultureInfo.InvariantCulture),
            Currency = pricing.Currency,
            IsAvailable = pricing.IsAvailable,
            TracksInventory = pricing.TracksInventory,
            StockQuantity = pricing.StockQuantity ?? 0,
            IsPurchasable = pricing.IsPurchasable,
        };
    }
}
