using MassTransit;
using Microsoft.Extensions.Options;
using Rappix.Contracts.Dispatch;
using Rappix.Orders.Application.Configuration;
using Rappix.Orders.Application.Sagas.Messages;
using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Infrastructure.Messaging.Simulation;

/// <summary>
/// Responder de entrega SIMULADO (borrar cuando llegue Dispatch, Fase 6). Reacciona al cambio de estado a
/// InProgress (garantiza que el pedido ya esta en curso, sin carrera con el commit) y, si AutoDeliver esta
/// activo, publica OrderDelivered para llevar la saga a Completed. Asi la demo llega a Completed sin Dispatch.
/// </summary>
internal sealed class SimulatedDeliveryResponder(IOptions<OrdersOptions> options)
    : IConsumer<OrderStatusChanged>
{
    public async Task Consume(ConsumeContext<OrderStatusChanged> context)
    {
        OrdersOptions.SimulationOptions simulation = options.Value.Simulation;
        if (!simulation.AutoDeliver || context.Message.Status != OrderStatus.InProgress)
        {
            return;
        }

        if (simulation.DeliveryDelayMs > 0)
        {
            await Task.Delay(simulation.DeliveryDelayMs, context.CancellationToken);
        }

        await context.Publish(new OrderDeliveredIntegrationEvent { OrderId = context.Message.OrderId, DeliveredAtUtc = DateTime.UtcNow });
    }
}
