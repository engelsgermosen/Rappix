using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Responses;

namespace Rappix.Merchants.Application.Merchants.GetActivation;

/// <summary>Consulta interna (gRPC) del estado de activacion de un merchant por Id.</summary>
public sealed record GetMerchantActivationQuery(Guid MerchantId) : IRequest<Result<MerchantActivationResponse>>;
