namespace Rappix.Orders.Application.Abstractions;

/// <summary>Cliente del servicio Catalog (gRPC) para el patron de reserva de stock: reservar/confirmar/liberar.</summary>
public interface IStockReservationClient
{
    /// <summary>Reserva (hold) stock para el pedido, todo-o-nada. Idempotente por orderId.</summary>
    Task<StockOperationResult> ReserveAsync(Guid orderId, IReadOnlyList<StockReservationLineInput> lines, int ttlSeconds, CancellationToken cancellationToken);

    /// <summary>Confirma los holds del pedido (decremento definitivo). Idempotente.</summary>
    Task<StockOperationResult> CommitAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Libera los holds del pedido (compensacion). Idempotente.</summary>
    Task<StockOperationResult> ReleaseAsync(Guid orderId, CancellationToken cancellationToken);
}

/// <summary>Una linea a reservar: item y cantidad.</summary>
public sealed record StockReservationLineInput(Guid ItemId, int Quantity);

/// <summary>Resultado de una operacion de stock. ServiceAvailable=false indica que gRPC no respondio.</summary>
public sealed record StockOperationResult(bool ServiceAvailable, bool Success, string ErrorCode)
{
    /// <summary>Resultado cuando el servicio Catalog no esta disponible (la saga reintentara).</summary>
    public static readonly StockOperationResult Unavailable = new(ServiceAvailable: false, Success: false, "Catalog.Unavailable");
}
