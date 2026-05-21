using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Responses;

namespace Rappix.Merchants.Application.Admin.UpdateCommission;

/// <summary>Un admin ajusta la comision negociada de un merchant.</summary>
public sealed record UpdateCommissionCommand(Guid MerchantId, decimal CommissionPercentage)
    : IRequest<Result<MerchantResponse>>;
