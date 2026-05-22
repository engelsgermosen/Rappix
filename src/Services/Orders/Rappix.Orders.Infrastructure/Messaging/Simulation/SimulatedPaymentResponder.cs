using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rappix.Contracts.Payments;
using Rappix.Orders.Application.Configuration;

namespace Rappix.Orders.Infrastructure.Messaging.Simulation;

/// <summary>
/// Responder de pago SIMULADO (borrar cuando llegue Payments, Fase 8). Atiende PaymentRequested y responde
/// segun Orders:Simulation:Payment:Outcome (Success | Fail | Timeout). Timeout = no responde, para que dispare
/// el timeout de pago de la saga. Tambien atiende RefundRequested (lo registra como completado).
/// </summary>
internal sealed partial class SimulatedPaymentResponder(IOptions<OrdersOptions> options, ILogger<SimulatedPaymentResponder> logger)
    : IConsumer<PaymentRequestedIntegrationEvent>, IConsumer<RefundRequestedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<PaymentRequestedIntegrationEvent> context)
    {
        PaymentRequestedIntegrationEvent message = context.Message;
        OrdersOptions.SimulationOptions simulation = options.Value.Simulation;
        if (simulation.PaymentDelayMs > 0)
        {
            await Task.Delay(simulation.PaymentDelayMs, context.CancellationToken);
        }

        switch (simulation.PaymentOutcome.Trim().ToLowerInvariant())
        {
            case "fail":
                await context.Publish(new PaymentFailedIntegrationEvent { OrderId = message.OrderId, Reason = "Pago rechazado (simulado)" });
                break;
            case "timeout":
                LogTimeout(logger, message.OrderId);
                break;
            default:
                await context.Publish(new PaymentSucceededIntegrationEvent { OrderId = message.OrderId, PaymentId = Guid.CreateVersion7(), Amount = message.Amount });
                break;
        }
    }

    public Task Consume(ConsumeContext<RefundRequestedIntegrationEvent> context)
    {
        RefundRequestedIntegrationEvent message = context.Message;
        LogRefund(logger, message.OrderId, message.Amount, message.Currency);
        return context.Publish(new RefundCompletedIntegrationEvent { OrderId = message.OrderId, RefundId = Guid.CreateVersion7() });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pago simulado en modo Timeout para el pedido {OrderId}: no se responde.")]
    private static partial void LogTimeout(ILogger logger, Guid orderId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reembolso simulado del pedido {OrderId} por {Amount} {Currency}.")]
    private static partial void LogRefund(ILogger logger, Guid orderId, decimal amount, string currency);
}
