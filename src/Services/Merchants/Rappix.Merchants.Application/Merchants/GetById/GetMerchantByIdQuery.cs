using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Responses;

namespace Rappix.Merchants.Application.Merchants.GetById;

/// <summary>Vista publica de un merchant por Id (solo si esta Active).</summary>
public sealed record GetMerchantByIdQuery(Guid MerchantId) : IRequest<Result<PublicMerchantResponse>>;
