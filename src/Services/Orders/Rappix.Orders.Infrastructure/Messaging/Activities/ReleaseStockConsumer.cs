using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Application.Sagas.Messages;

namespace Rappix.Orders.Infrastructure.Messaging.Activities;

/// <summary>
/// Activity de la saga (compensacion): libera el stock reservado via gRPC a Catalog. Best-effort: si el
/// servicio no responde, reintenta; si la operacion falla por otra causa, igual publica StockReleased para
/// no bloquear la compensacion (release es idempotente).
/// </summary>
internal sealed class ReleaseStockConsumer(IStockReservationClient stock, ILogger<ReleaseStockConsumer> logger)
    : IConsumer<ReleaseStock>
{
    public async Task Consume(ConsumeContext<ReleaseStock> context)
    {
        ReleaseStock message = context.Message;
        StockOperationResult result = await stock.ReleaseAsync(message.OrderId, context.CancellationToken);

        if (!result.ServiceAvailable)
        {
            throw new InvalidOperationException($"Catalog no disponible al liberar stock del pedido {message.OrderId}.");
        }

        if (!result.Success)
        {
            logger.LogWarning("La liberacion de stock del pedido {OrderId} reporto {ErrorCode}; se continua la compensacion.", message.OrderId, result.ErrorCode);
        }

        await context.Publish(new StockReleased(message.OrderId));
    }
}
