using MassTransit;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Application.Sagas.Messages;
using Rappix.Orders.Domain.Errors;
using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Application.Orders.Reject;

/// <summary>Valida la propiedad y publica MerchantRejected (la saga compensa y cancela).</summary>
internal sealed class RejectOrderCommandHandler(IOrderRepository orders, IPublishEndpoint publishEndpoint)
    : IRequestHandler<RejectOrderCommand, Result>
{
    public async Task<Result> Handle(RejectOrderCommand command, CancellationToken cancellationToken)
    {
        Order? order = await orders.GetByIdAsync(new OrderId(command.OrderId), cancellationToken);
        if (order is null)
        {
            return Result.Failure(OrderErrors.NotFound);
        }

        if (order.MerchantOwnerUserId != command.MerchantUserId)
        {
            return Result.Failure(OrderErrors.NotForMerchant);
        }

        if (order.IsTerminal)
        {
            return Result.Failure(OrderErrors.InvalidState);
        }

        string reason = string.IsNullOrWhiteSpace(command.Reason) ? "Rechazado por el merchant" : command.Reason;
        await publishEndpoint.Publish(new MerchantRejected(command.OrderId, reason), cancellationToken);
        return Result.Success();
    }
}
