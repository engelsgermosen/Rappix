using Grpc.Core;
using Microsoft.Extensions.Logging;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Infrastructure.Grpc.Merchants;

namespace Rappix.Pricing.Infrastructure.Grpc;

/// <summary>
/// Adaptador del puerto <see cref="IMerchantPricingClient"/> sobre el cliente gRPC de Merchants. La
/// resiliencia la añade AddStandardResilienceHandler en el registro DI; si aun asi falla, devuelve
/// <see cref="MerchantPricingInfo.Unavailable"/> y el llamador procede sin bloquear la cotizacion.
/// </summary>
internal sealed partial class MerchantPricingGrpcClient(
    MerchantValidationService.MerchantValidationServiceClient client,
    ILogger<MerchantPricingGrpcClient> logger)
    : IMerchantPricingClient
{
    public async Task<MerchantPricingInfo> GetAsync(Guid merchantId, CancellationToken cancellationToken)
    {
        try
        {
            MerchantBasicInfoResponse response = await client.GetMerchantBasicInfoAsync(
                new MerchantIdRequest { MerchantId = merchantId.ToString() },
                cancellationToken: cancellationToken);

            return new MerchantPricingInfo(
                ServiceAvailable: true,
                Found: response.Found,
                IsActive: response.IsActive,
                VerticalType: response.VerticalType);
        }
        catch (RpcException ex)
        {
            LogUnavailable(logger, merchantId, ex.StatusCode, ex);
            return MerchantPricingInfo.Unavailable;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "El servicio Merchants no respondio para {MerchantId} ({StatusCode}); usando fallback.")]
    private static partial void LogUnavailable(ILogger logger, Guid merchantId, StatusCode statusCode, Exception exception);
}
