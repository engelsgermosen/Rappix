using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Application.Sagas.Messages;
using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Infrastructure.Messaging.Activities;

/// <summary>
/// Activity de la saga: reserva (hold) el stock del pedido via gRPC a Catalog. Carga las lineas del pedido
/// del repositorio (el snapshot ya esta persistido) y publica el resultado.
/// </summary>
internal sealed class ReserveStockConsumer(IStockReservationClient stock, IOrderRepository orders, ILogger<ReserveStockConsumer> logger)
    : IConsumer<ReserveStock>
{
    public async Task Consume(ConsumeContext<ReserveStock> context)
    {
        ReserveStock message = context.Message;

        Order? order = await orders.GetByIdAsync(new OrderId(message.OrderId), context.CancellationToken);
        if (order is null)
        {
            await context.Publish(new StockReservationFailed(message.OrderId, "Orders.Order.NotFound"));
            return;
        }

        var lines = order.Lines.Select(line => new StockReservationLineInput(line.ItemId, line.Quantity)).ToList();
        StockOperationResult result = await stock.ReserveAsync(message.OrderId, lines, message.TtlSeconds, context.CancellationToken);

        if (!result.ServiceAvailable)
        {
            throw new InvalidOperationException($"Catalog no disponible al reservar stock del pedido {message.OrderId}.");
        }

        if (result.Success)
        {
            await context.Publish(new StockReserved(message.OrderId));
        }
        else
        {
            logger.LogWarning("No se pudo reservar stock del pedido {OrderId}: {ErrorCode}", message.OrderId, result.ErrorCode);
            await context.Publish(new StockReservationFailed(message.OrderId, result.ErrorCode));
        }
    }
}
