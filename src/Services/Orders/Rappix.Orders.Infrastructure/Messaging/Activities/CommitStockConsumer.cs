using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Application.Sagas.Messages;

namespace Rappix.Orders.Infrastructure.Messaging.Activities;

/// <summary>
/// Activity de la saga: confirma (decremento definitivo) el stock reservado via gRPC a Catalog. Si el
/// servicio no responde, lanza para reintentar (la reserva ya garantiza el inventario).
/// </summary>
internal sealed class CommitStockConsumer(IStockReservationClient stock, ILogger<CommitStockConsumer> logger)
    : IConsumer<CommitStock>
{
    public async Task Consume(ConsumeContext<CommitStock> context)
    {
        CommitStock message = context.Message;
        StockOperationResult result = await stock.CommitAsync(message.OrderId, context.CancellationToken);

        if (!result.ServiceAvailable)
        {
            throw new InvalidOperationException($"Catalog no disponible al confirmar stock del pedido {message.OrderId}.");
        }

        if (result.Success)
        {
            await context.Publish(new StockCommitted(message.OrderId));
        }
        else
        {
            logger.LogError("Fallo al confirmar stock del pedido {OrderId}: {ErrorCode}. Requiere revision manual.", message.OrderId, result.ErrorCode);
            await context.Publish(new StockCommitFailed(message.OrderId, result.ErrorCode));
        }
    }
}
