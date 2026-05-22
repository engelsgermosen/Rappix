using MassTransit;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Application.Sagas.Messages;
using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Infrastructure.Messaging.Projections;

/// <summary>
/// Proyeccion: aplica al agregado Order el estado que la saga publica en cada transicion (OrderStatusChanged),
/// manteniendo el espejo que ven cliente y merchant. Idempotente: ApplyStatus no retrocede desde un terminal.
/// </summary>
internal sealed class OrderStatusProjectionConsumer(IOrderRepository orders, IUnitOfWork unitOfWork)
    : IConsumer<OrderStatusChanged>
{
    public async Task Consume(ConsumeContext<OrderStatusChanged> context)
    {
        OrderStatusChanged message = context.Message;

        Order? order = await orders.GetByIdAsync(new OrderId(message.OrderId), context.CancellationToken);
        if (order is null)
        {
            return;
        }

        order.ApplyStatus(message.Status, message.ChangedAtUtc, message.Reason);
        await unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
}
