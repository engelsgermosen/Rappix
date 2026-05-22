using System.Globalization;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Infrastructure.Grpc.Catalog;

namespace Rappix.Pricing.Infrastructure.Grpc;

/// <summary>
/// Adaptador del puerto <see cref="ICatalogPricingClient"/> sobre el cliente gRPC de Catalog. La resiliencia
/// (retry + circuit breaker) la añade AddStandardResilienceHandler en el registro DI; si aun asi la llamada
/// falla, devuelve <see cref="CatalogItemPricing.Unavailable"/> para que el llamador haga fallback al cache.
/// El precio viaja como string en el contrato gRPC para preservar la precision decimal.
/// </summary>
internal sealed partial class CatalogPricingGrpcClient(
    CatalogValidationService.CatalogValidationServiceClient client,
    ILogger<CatalogPricingGrpcClient> logger)
    : ICatalogPricingClient
{
    public async Task<CatalogItemPricing> GetItemPricingAsync(Guid itemId, CancellationToken cancellationToken)
    {
        try
        {
            ItemPricingResponse response = await client.GetItemPricingAsync(
                new ItemPricingRequest { ItemId = itemId.ToString() },
                cancellationToken: cancellationToken);

            if (!response.Found)
            {
                return new CatalogItemPricing(
                    ServiceAvailable: true, Found: false, itemId, Guid.Empty, string.Empty, 0m, "DOP", IsPurchasable: false);
            }

            decimal price = decimal.TryParse(response.PriceAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
                ? parsed
                : 0m;
            Guid merchantId = Guid.TryParse(response.MerchantId, out Guid parsedMerchant) ? parsedMerchant : Guid.Empty;

            return new CatalogItemPricing(
                ServiceAvailable: true,
                Found: true,
                itemId,
                merchantId,
                response.Name,
                price,
                response.Currency,
                response.IsPurchasable);
        }
        catch (RpcException ex)
        {
            LogUnavailable(logger, itemId, ex.StatusCode, ex);
            return CatalogItemPricing.Unavailable;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "El servicio Catalog no respondio para el item {ItemId} ({StatusCode}); usando fallback al cache local.")]
    private static partial void LogUnavailable(ILogger logger, Guid itemId, StatusCode statusCode, Exception exception);
}
