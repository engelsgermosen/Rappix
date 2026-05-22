using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Orders.Application.Orders.Accept;

/// <summary>El merchant acepta el pedido. Publica MerchantAccepted; la saga avanza al cobro.
/// MerchantUserId es el sub del JWT del merchant; se compara contra Order.MerchantOwnerUserId.</summary>
public sealed record AcceptOrderCommand(Guid OrderId, Guid MerchantUserId) : IRequest<Result>;
