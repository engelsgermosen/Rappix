using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Merchants.Application.Admin.Unsuspend;

/// <summary>Un admin reactiva un merchant suspendido (Suspended -> Active).</summary>
public sealed record UnsuspendMerchantCommand(Guid MerchantId) : IRequest<Result>;
