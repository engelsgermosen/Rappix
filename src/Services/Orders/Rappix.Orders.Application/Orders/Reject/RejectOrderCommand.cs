using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Orders.Application.Orders.Reject;

/// <summary>El merchant rechaza el pedido. Publica MerchantRejected; la saga compensa y cancela.
/// MerchantUserId es el sub del JWT del merchant; se compara contra Order.MerchantOwnerUserId.</summary>
public sealed record RejectOrderCommand(Guid OrderId, Guid MerchantUserId, string Reason) : IRequest<Result>;
