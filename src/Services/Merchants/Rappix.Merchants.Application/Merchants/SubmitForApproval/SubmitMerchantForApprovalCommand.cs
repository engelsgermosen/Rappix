using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Merchants.Application.Merchants.SubmitForApproval;

/// <summary>El owner envia su merchant a aprobacion (Draft -> Pending).</summary>
public sealed record SubmitMerchantForApprovalCommand(Guid OwnerUserId) : IRequest<Result>;
