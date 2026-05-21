using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Responses;

namespace Rappix.Merchants.Application.Merchants.UpdateProfile;

/// <summary>Actualiza el perfil del merchant del owner autenticado.</summary>
public sealed record UpdateMerchantProfileCommand(
    Guid OwnerUserId,
    string Name,
    string Slug,
    string? Rnc,
    string? Description,
    string VerticalType) : IRequest<Result<MerchantResponse>>;
