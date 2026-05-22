using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Application.Responses;
using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Application.Orders.MerchantPending;

/// <summary>Lista los pedidos del merchant en estado AwaitingMerchant (los que debe aceptar o rechazar).</summary>
internal sealed class ListMerchantPendingQueryHandler(IOrderRepository orders)
    : IRequestHandler<ListMerchantPendingQuery, Result<IReadOnlyList<OrderResponse>>>
{
    public async Task<Result<IReadOnlyList<OrderResponse>>> Handle(ListMerchantPendingQuery query, CancellationToken cancellationToken)
    {
        int pageSize = Math.Clamp(query.PageSize, 1, 100);
        int skip = Math.Max(query.Page - 1, 0) * pageSize;

        IReadOnlyList<Order> pending = await orders.ListByMerchantOwnerAndStatusAsync(query.MerchantUserId, OrderStatus.AwaitingMerchant, skip, pageSize, cancellationToken);
        return Result.Success<IReadOnlyList<OrderResponse>>([.. pending.Select(OrderResponse.From)]);
    }
}
