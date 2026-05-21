using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Merchants.Application.Admin.Reject;

/// <summary>Un admin rechaza la aprobacion de un merchant (Pending -> Rejected).</summary>
public sealed record RejectMerchantCommand(Guid MerchantId, string Reason) : IRequest<Result>;
