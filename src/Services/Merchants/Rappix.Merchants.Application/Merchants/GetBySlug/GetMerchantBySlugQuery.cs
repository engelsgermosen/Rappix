using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Responses;

namespace Rappix.Merchants.Application.Merchants.GetBySlug;

/// <summary>Vista publica de un merchant por slug (solo si esta Active).</summary>
public sealed record GetMerchantBySlugQuery(string Slug) : IRequest<Result<PublicMerchantResponse>>;
