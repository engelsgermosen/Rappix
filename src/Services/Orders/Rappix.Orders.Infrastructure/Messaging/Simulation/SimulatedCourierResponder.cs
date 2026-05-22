using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rappix.Contracts.Dispatch;
using Rappix.Orders.Application.Configuration;

namespace Rappix.Orders.Infrastructure.Messaging.Simulation;

/// <summary>
/// Responder de courier SIMULADO (borrar cuando llegue Dispatch, Fase 6). Atiende CourierRequested y responde
/// segun Orders:Simulation:Courier:Outcome (Success | Unavailable | Timeout). Timeout = no responde, para que
/// dispare el timeout de courier de la saga.
/// </summary>
internal sealed partial class SimulatedCourierResponder(IOptions<OrdersOptions> options, ILogger<SimulatedCourierResponder> logger)
    : IConsumer<CourierRequestedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<CourierRequestedIntegrationEvent> context)
    {
        CourierRequestedIntegrationEvent message = context.Message;
        OrdersOptions.SimulationOptions simulation = options.Value.Simulation;
        if (simulation.CourierDelayMs > 0)
        {
            await Task.Delay(simulation.CourierDelayMs, context.CancellationToken);
        }

        switch (simulation.CourierOutcome.Trim().ToLowerInvariant())
        {
            case "unavailable":
                await context.Publish(new CourierUnavailableIntegrationEvent { OrderId = message.OrderId, Reason = "Sin couriers disponibles (simulado)" });
                break;
            case "timeout":
                LogTimeout(logger, message.OrderId);
                break;
            default:
                await context.Publish(new CourierAssignedIntegrationEvent { OrderId = message.OrderId, CourierId = Guid.CreateVersion7() });
                break;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Courier simulado en modo Timeout para el pedido {OrderId}: no se responde.")]
    private static partial void LogTimeout(ILogger logger, Guid orderId);
}
