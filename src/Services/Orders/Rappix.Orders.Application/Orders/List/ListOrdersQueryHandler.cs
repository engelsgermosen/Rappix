using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Application.Responses;
using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Application.Orders.List;

/// <summary>Lista los pedidos del cliente con paginacion acotada.</summary>
internal sealed class ListOrdersQueryHandler(IOrderRepository orders)
    : IRequestHandler<ListOrdersQuery, Result<IReadOnlyList<OrderResponse>>>
{
    public async Task<Result<IReadOnlyList<OrderResponse>>> Handle(ListOrdersQuery query, CancellationToken cancellationToken)
    {
        int pageSize = Math.Clamp(query.PageSize, 1, 100);
        int skip = Math.Max(query.Page - 1, 0) * pageSize;

        IReadOnlyList<Order> orderList = await orders.ListByCustomerAsync(query.CustomerUserId, skip, pageSize, cancellationToken);
        return Result.Success<IReadOnlyList<OrderResponse>>([.. orderList.Select(OrderResponse.From)]);
    }
}
