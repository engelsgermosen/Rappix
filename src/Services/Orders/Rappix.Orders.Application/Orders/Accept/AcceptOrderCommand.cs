using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Orders.Application.Orders.Accept;

/// <summary>El merchant acepta el pedido. Publica MerchantAccepted; la saga avanza al cobro.</summary>
public sealed record AcceptOrderCommand(Guid OrderId, Guid MerchantId) : IRequest<Result>;
