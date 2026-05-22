using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Orders.Application.Orders.Reject;

/// <summary>El merchant rechaza el pedido. Publica MerchantRejected; la saga compensa y cancela.</summary>
public sealed record RejectOrderCommand(Guid OrderId, Guid MerchantId, string Reason) : IRequest<Result>;
