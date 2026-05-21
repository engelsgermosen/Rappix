using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Merchants.Application.Admin.Approve;

/// <summary>Un admin aprueba un merchant (Pending -> Active).</summary>
public sealed record ApproveMerchantCommand(Guid MerchantId) : IRequest<Result>;
