using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Application.Responses;
using Rappix.Orders.Domain.Errors;
using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Application.Orders.Get;

/// <summary>Recupera un pedido validando que pertenezca al cliente que lo solicita.</summary>
internal sealed class GetOrderQueryHandler(IOrderRepository orders)
    : IRequestHandler<GetOrderQuery, Result<OrderResponse>>
{
    public async Task<Result<OrderResponse>> Handle(GetOrderQuery query, CancellationToken cancellationToken)
    {
        Order? order = await orders.GetByIdAsync(new OrderId(query.OrderId), cancellationToken);
        if (order is null)
        {
            return Result.Failure<OrderResponse>(OrderErrors.NotFound);
        }

        return order.CustomerUserId != query.CustomerUserId
            ? Result.Failure<OrderResponse>(OrderErrors.NotOwnedByCustomer)
            : OrderResponse.From(order);
    }
}
