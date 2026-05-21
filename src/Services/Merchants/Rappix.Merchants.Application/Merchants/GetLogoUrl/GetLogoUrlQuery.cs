using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Merchants.Application.Merchants.GetLogoUrl;

/// <summary>Devuelve una URL firmada del logo de un merchant Active.</summary>
public sealed record GetLogoUrlQuery(Guid MerchantId) : IRequest<Result<string>>;
