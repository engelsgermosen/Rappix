using Grpc.Core;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Merchants.GetActivation;
using Rappix.Merchants.Application.Responses;

namespace Rappix.Merchants.Api.Grpc;

/// <summary>Implementacion gRPC del servicio interno de validacion de comercios. Delega en MediatR.</summary>
internal sealed class MerchantValidationServiceImpl(ISender sender)
    : MerchantValidationService.MerchantValidationServiceBase
{
    public override async Task<IsMerchantActiveResponse> IsMerchantActive(MerchantIdRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.MerchantId, out Guid merchantId))
        {
            return new IsMerchantActiveResponse { IsActive = false, Status = "Invalid" };
        }

        Result<MerchantActivationResponse> result = await sender.Send(new GetMerchantActivationQuery(merchantId), context.CancellationToken);
        if (result.IsFailure)
        {
            return new IsMerchantActiveResponse { IsActive = false, Status = "Unknown" };
        }

        return new IsMerchantActiveResponse { IsActive = result.Value.IsActive, Status = result.Value.Status };
    }

    public override async Task<MerchantBasicInfoResponse> GetMerchantBasicInfo(MerchantIdRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.MerchantId, out Guid merchantId))
        {
            return new MerchantBasicInfoResponse { Found = false };
        }

        Result<MerchantActivationResponse> result = await sender.Send(new GetMerchantActivationQuery(merchantId), context.CancellationToken);
        if (result.IsFailure || !result.Value.Found)
        {
            return new MerchantBasicInfoResponse { Found = false };
        }

        MerchantActivationResponse info = result.Value;
        return new MerchantBasicInfoResponse
        {
            Found = true,
            IsActive = info.IsActive,
            Name = info.Name,
            Slug = info.Slug,
            VerticalType = info.VerticalType,
        };
    }
}
