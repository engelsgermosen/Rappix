using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Orders.Application.Responses;

namespace Rappix.Orders.Application.Orders.Get;

/// <summary>Obtiene un pedido del cliente por id (solo si le pertenece).</summary>
public sealed record GetOrderQuery(Guid OrderId, Guid CustomerUserId) : IRequest<Result<OrderResponse>>;
