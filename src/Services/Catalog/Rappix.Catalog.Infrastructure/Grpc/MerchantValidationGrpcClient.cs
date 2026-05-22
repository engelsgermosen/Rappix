using Grpc.Core;
using Microsoft.Extensions.Logging;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Infrastructure.Grpc.Merchants;

namespace Rappix.Catalog.Infrastructure.Grpc;

/// <summary>
/// Adaptador del puerto <see cref="IMerchantValidationClient"/> sobre el cliente gRPC de Merchants.
/// La resiliencia (retry + circuit breaker) la añade AddStandardResilienceHandler en el registro DI;
/// si aun asi la llamada falla (circuito abierto / timeout), devuelve <see cref="MerchantValidation.Unavailable"/>
/// para que el llamador haga fallback al gating local cacheado.
/// </summary>
internal sealed partial class MerchantValidationGrpcClient(
    MerchantValidationService.MerchantValidationServiceClient client,
    ILogger<MerchantValidationGrpcClient> logger)
    : IMerchantValidationClient
{
    public async Task<MerchantValidation> ValidateAsync(Guid merchantId, CancellationToken cancellationToken)
    {
        try
        {
            IsMerchantActiveResponse response = await client.IsMerchantActiveAsync(
                new MerchantIdRequest { MerchantId = merchantId.ToString() },
                cancellationToken: cancellationToken);

            bool exists = !string.Equals(response.Status, "Unknown", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(response.Status, "Invalid", StringComparison.OrdinalIgnoreCase);

            return new MerchantValidation(ServiceAvailable: true, Exists: exists, IsActive: response.IsActive);
        }
        catch (RpcException ex)
        {
            LogUnavailable(logger, merchantId, ex.StatusCode, ex);
            return MerchantValidation.Unavailable;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "El servicio Merchants no respondio para {MerchantId} ({StatusCode}); usando fallback local.")]
    private static partial void LogUnavailable(ILogger logger, Guid merchantId, StatusCode statusCode, Exception exception);
}
