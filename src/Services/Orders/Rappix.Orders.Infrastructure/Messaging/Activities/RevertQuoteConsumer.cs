using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Application.Sagas.Messages;

namespace Rappix.Orders.Infrastructure.Messaging.Activities;

/// <summary>
/// Activity de la saga (compensacion): revierte el consumo de la cotizacion via gRPC a Pricing. Si el
/// servicio no responde, reintenta; si la reversion falla por otra causa, publica QuoteRevertFailed (la
/// saga finaliza igual, best-effort).
/// </summary>
internal sealed class RevertQuoteConsumer(IPricingClient pricing, ILogger<RevertQuoteConsumer> logger)
    : IConsumer<RevertQuote>
{
    public async Task Consume(ConsumeContext<RevertQuote> context)
    {
        RevertQuote message = context.Message;
        PricingOperationResult result = await pricing.RevertQuoteAsync(message.QuoteId, message.OrderId, message.Reason, context.CancellationToken);

        if (!result.ServiceAvailable)
        {
            throw new InvalidOperationException($"Pricing no disponible al revertir la cotizacion del pedido {message.OrderId}.");
        }

        if (result.Success)
        {
            await context.Publish(new QuoteReverted(message.OrderId));
        }
        else
        {
            logger.LogWarning("La reversion de la cotizacion del pedido {OrderId} reporto {ErrorCode}; se finaliza igual.", message.OrderId, result.ErrorCode);
            await context.Publish(new QuoteRevertFailed(message.OrderId, result.ErrorCode));
        }
    }
}
