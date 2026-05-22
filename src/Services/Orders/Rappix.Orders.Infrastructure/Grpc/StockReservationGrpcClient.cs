using Grpc.Core;
using Microsoft.Extensions.Logging;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Infrastructure.Grpc.Catalog;

namespace Rappix.Orders.Infrastructure.Grpc;

/// <summary>
/// Cliente gRPC del servicio de reserva de stock de Catalog. Ante RpcException devuelve ServiceAvailable=false
/// para que la saga reintente (no avanza ni compensa con un fallo transitorio).
/// </summary>
internal sealed partial class StockReservationGrpcClient(
    StockReservationService.StockReservationServiceClient client,
    ILogger<StockReservationGrpcClient> logger)
    : IStockReservationClient
{
    public async Task<StockOperationResult> ReserveAsync(Guid orderId, IReadOnlyList<StockReservationLineInput> lines, int ttlSeconds, CancellationToken cancellationToken)
    {
        var request = new ReserveStockRequest { OrderId = orderId.ToString(), TtlSeconds = ttlSeconds };
        request.Lines.AddRange(lines.Select(line => new ReserveStockLine { ItemId = line.ItemId.ToString(), Quantity = line.Quantity }));

        try
        {
            ReserveStockReply reply = await client.ReserveStockAsync(request, cancellationToken: cancellationToken);
            return new StockOperationResult(ServiceAvailable: true, reply.Success, reply.ErrorCode);
        }
        catch (RpcException ex)
        {
            LogUnavailable(logger, orderId, ex.StatusCode, ex);
            return StockOperationResult.Unavailable;
        }
    }

    public async Task<StockOperationResult> CommitAsync(Guid orderId, CancellationToken cancellationToken)
    {
        try
        {
            CommitStockReply reply = await client.CommitStockAsync(new CommitStockRequest { OrderId = orderId.ToString() }, cancellationToken: cancellationToken);
            return new StockOperationResult(ServiceAvailable: true, reply.Success, reply.ErrorCode);
        }
        catch (RpcException ex)
        {
            LogUnavailable(logger, orderId, ex.StatusCode, ex);
            return StockOperationResult.Unavailable;
        }
    }

    public async Task<StockOperationResult> ReleaseAsync(Guid orderId, CancellationToken cancellationToken)
    {
        try
        {
            ReleaseStockReply reply = await client.ReleaseStockAsync(new ReleaseStockRequest { OrderId = orderId.ToString() }, cancellationToken: cancellationToken);
            return new StockOperationResult(ServiceAvailable: true, reply.Success, reply.ErrorCode);
        }
        catch (RpcException ex)
        {
            LogUnavailable(logger, orderId, ex.StatusCode, ex);
            return StockOperationResult.Unavailable;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Catalog (reserva) no respondio para el pedido {OrderId} ({StatusCode}).")]
    private static partial void LogUnavailable(ILogger logger, Guid orderId, StatusCode statusCode, Exception exception);
}
