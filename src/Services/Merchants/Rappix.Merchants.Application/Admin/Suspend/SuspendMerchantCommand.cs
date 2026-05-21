using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Merchants.Application.Admin.Suspend;

/// <summary>Un admin suspende un merchant.</summary>
public sealed record SuspendMerchantCommand(Guid MerchantId, string Reason) : IRequest<Result>;
