using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Responses;

namespace Rappix.Merchants.Application.Merchants.UploadLogo;

/// <summary>Sube el logo del merchant del owner. El stream ya viene acotado a 2MB por el endpoint.</summary>
public sealed record UploadLogoCommand(Guid OwnerUserId, Stream Content) : IRequest<Result<MerchantResponse>>;
