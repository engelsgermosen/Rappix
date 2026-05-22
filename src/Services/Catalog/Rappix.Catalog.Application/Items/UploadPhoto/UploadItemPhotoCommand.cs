using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Responses;

namespace Rappix.Catalog.Application.Items.UploadPhoto;

/// <summary>Sube la foto de un item del merchant. El stream ya viene acotado de tamano por el endpoint.</summary>
public sealed record UploadItemPhotoCommand(Guid MerchantId, Guid ItemId, Stream Content) : IRequest<Result<ItemResponse>>;
