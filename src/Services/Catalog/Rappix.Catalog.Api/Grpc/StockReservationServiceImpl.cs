using Grpc.Core;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Stock.Commit;
using Rappix.Catalog.Application.Stock.Release;
using Rappix.Catalog.Application.Stock.Reserve;
using AppReserveLine = Rappix.Catalog.Application.Stock.Reserve.ReserveStockLine;

namespace Rappix.Catalog.Api.Grpc;

/// <summary>
/// Implementacion gRPC del patron de reserva de stock (Reserve/Commit/Release) que consume la saga de
/// Orders. Delega en MediatR; todas las operaciones son idempotentes por order_id.
/// </summary>
internal sealed class StockReservationServiceImpl(ISender sender)
    : StockReservationService.StockReservationServiceBase
{
    private const string InvalidRequest = "Catalog.Reservation.InvalidRequest";

    public override async Task<ReserveStockReply> ReserveStock(ReserveStockRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out Guid orderId))
        {
            return new ReserveStockReply { Success = false, ErrorCode = InvalidRequest };
        }

        var lines = new List<AppReserveLine>(request.Lines.Count);
        foreach (ReserveStockLine line in request.Lines)
        {
            if (!Guid.TryParse(line.ItemId, out Guid itemId))
            {
                return new ReserveStockReply { Success = false, ErrorCode = InvalidRequest };
            }

            lines.Add(new AppReserveLine(itemId, line.Quantity));
        }

        Result result = await sender.Send(new ReserveStockCommand(orderId, lines, request.TtlSeconds), context.CancellationToken);
        return result.IsSuccess
            ? new ReserveStockReply { Success = true }
            : new ReserveStockReply { Success = false, ErrorCode = result.Error.Code };
    }

    public override async Task<CommitStockReply> CommitStock(CommitStockRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out Guid orderId))
        {
            return new CommitStockReply { Success = false, ErrorCode = InvalidRequest };
        }

        Result result = await sender.Send(new CommitStockCommand(orderId), context.CancellationToken);
        return result.IsSuccess
            ? new CommitStockReply { Success = true }
            : new CommitStockReply { Success = false, ErrorCode = result.Error.Code };
    }

    public override async Task<ReleaseStockReply> ReleaseStock(ReleaseStockRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.OrderId, out Guid orderId))
        {
            return new ReleaseStockReply { Success = false, ErrorCode = InvalidRequest };
        }

        Result result = await sender.Send(new ReleaseStockCommand(orderId), context.CancellationToken);
        return result.IsSuccess
            ? new ReleaseStockReply { Success = true }
            : new ReleaseStockReply { Success = false, ErrorCode = result.Error.Code };
    }
}
