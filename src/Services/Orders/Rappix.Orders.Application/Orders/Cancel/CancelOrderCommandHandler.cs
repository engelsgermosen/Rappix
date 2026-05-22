using MassTransit;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Application.Sagas.Messages;
using Rappix.Orders.Domain.Errors;
using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Application.Orders.Cancel;

/// <summary>
/// Valida la propiedad y que el pedido aun sea cancelable (no paso el punto de no retorno: Committing en
/// adelante), y publica OrderCancellationRequested. La saga compensa segun su estado real. El chequeo de
/// cancelabilidad usa la proyeccion (consistencia eventual): una pequena ventana es aceptable y documentada.
/// </summary>
internal sealed class CancelOrderCommandHandler(IOrderRepository orders, IPublishEndpoint publishEndpoint)
    : IRequestHandler<CancelOrderCommand, Result>
{
    public async Task<Result> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        Order? order = await orders.GetByIdAsync(new OrderId(command.OrderId), cancellationToken);
        if (order is null)
        {
            return Result.Failure(OrderErrors.NotFound);
        }

        if (order.CustomerUserId != command.CustomerUserId)
        {
            return Result.Failure(OrderErrors.NotOwnedByCustomer);
        }

        if (!order.IsCancellable)
        {
            return Result.Failure(OrderErrors.NotCancellable);
        }

        string reason = string.IsNullOrWhiteSpace(command.Reason) ? "Cancelado por el cliente" : command.Reason;
        await publishEndpoint.Publish(new OrderCancellationRequested(command.OrderId, reason), cancellationToken);
        return Result.Success();
    }
}
