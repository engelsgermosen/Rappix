using Grpc.Core;
using Microsoft.Extensions.Logging;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Infrastructure.Grpc.Merchants;

namespace Rappix.Orders.Infrastructure.Grpc;

/// <summary>
/// Cliente gRPC del servicio Merchants. Resuelve el merchant (incluido su OwnerUserId) por id. Ante
/// RpcException devuelve ServiceAvailable=false para que el caso de uso falle de forma controlada.
/// </summary>
internal sealed partial class MerchantValidationGrpcClient(
    MerchantValidationService.MerchantValidationServiceClient client,
    ILogger<MerchantValidationGrpcClient> logger)
    : IMerchantValidationClient
{
    public async Task<MerchantInfo> GetAsync(Guid merchantId, CancellationToken cancellationToken)
    {
        try
        {
            MerchantBasicInfoResponse response = await client.GetMerchantBasicInfoAsync(
                new MerchantIdRequest { MerchantId = merchantId.ToString() },
                cancellationToken: cancellationToken);

            Guid ownerUserId = Guid.TryParse(response.OwnerUserId, out Guid parsed) ? parsed : Guid.Empty;
            return new MerchantInfo(
                ServiceAvailable: true,
                Found: response.Found,
                IsActive: response.IsActive,
                OwnerUserId: ownerUserId,
                // El .proto ya devuelve Name (campo 3); antes de Fase 13.6 lo descartabamos. Ahora viaja
                // al snapshot del CourierAssignment para que el courier vea "Recoger en {merchantName}".
                Name: response.Name ?? string.Empty,
                PickupLatitude: response.PickupLatitude,
                PickupLongitude: response.PickupLongitude,
                HasPickupLocation: response.HasPickupLocation);
        }
        catch (RpcException ex)
        {
            LogUnavailable(logger, merchantId, ex.StatusCode, ex);
            return MerchantInfo.Unavailable;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Merchants no respondio para el merchant {MerchantId} ({StatusCode}).")]
    private static partial void LogUnavailable(ILogger logger, Guid merchantId, StatusCode statusCode, Exception exception);
}
