using MassTransit;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Application.Sagas.Messages;
using Rappix.Orders.Domain.Errors;
using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Application.Orders.Accept;

/// <summary>
/// Valida la propiedad y que el pedido no sea terminal, y publica MerchantAccepted. La saga decide segun su
/// estado real (descarta el evento si ya no esta esperando al merchant), evitando carreras con la proyeccion.
/// </summary>
internal sealed class AcceptOrderCommandHandler(IOrderRepository orders, IPublishEndpoint publishEndpoint)
    : IRequestHandler<AcceptOrderCommand, Result>
{
    public async Task<Result> Handle(AcceptOrderCommand command, CancellationToken cancellationToken)
    {
        Order? order = await orders.GetByIdAsync(new OrderId(command.OrderId), cancellationToken);
        if (order is null)
        {
            return Result.Failure(OrderErrors.NotFound);
        }

        if (order.MerchantId != command.MerchantId)
        {
            return Result.Failure(OrderErrors.NotForMerchant);
        }

        if (order.IsTerminal)
        {
            return Result.Failure(OrderErrors.InvalidState);
        }

        await publishEndpoint.Publish(new MerchantAccepted(command.OrderId), cancellationToken);
        return Result.Success();
    }
}
