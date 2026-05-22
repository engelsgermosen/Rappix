using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Application.Sagas.Messages;

namespace Rappix.Orders.Infrastructure.Messaging.Activities;

/// <summary>
/// Activity de la saga: consume la cotizacion via gRPC a Pricing y publica el resultado. Si el servicio no
/// responde, lanza para que el bus reintente (no avanza la saga con un fallo transitorio).
/// </summary>
internal sealed class ConsumeQuoteConsumer(IPricingClient pricing, ILogger<ConsumeQuoteConsumer> logger)
    : IConsumer<ConsumeQuote>
{
    public async Task Consume(ConsumeContext<ConsumeQuote> context)
    {
        ConsumeQuote message = context.Message;
        PricingOperationResult result = await pricing.ConsumeQuoteAsync(message.QuoteId, message.OrderId, context.CancellationToken);

        if (!result.ServiceAvailable)
        {
            throw new InvalidOperationException($"Pricing no disponible al consumir la cotizacion del pedido {message.OrderId}.");
        }

        if (result.Success)
        {
            await context.Publish(new QuoteConsumed(message.OrderId));
        }
        else
        {
            logger.LogWarning("No se pudo consumir la cotizacion del pedido {OrderId}: {ErrorCode}", message.OrderId, result.ErrorCode);
            await context.Publish(new QuoteConsumptionFailed(message.OrderId, result.ErrorCode));
        }
    }
}
