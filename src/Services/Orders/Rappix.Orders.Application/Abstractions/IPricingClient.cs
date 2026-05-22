namespace Rappix.Orders.Application.Abstractions;

/// <summary>Cliente del servicio Pricing (gRPC): leer/consumir/revertir la cotizacion que respalda el pedido.</summary>
public interface IPricingClient
{
    /// <summary>Obtiene la cotizacion completa (lineas + desglose) para armar el snapshot del pedido.</summary>
    Task<QuoteSnapshot> GetQuoteAsync(Guid quoteId, CancellationToken cancellationToken);

    /// <summary>Consume la cotizacion (congela precio, redime cupon). Idempotente por orderId.</summary>
    Task<PricingOperationResult> ConsumeQuoteAsync(Guid quoteId, Guid orderId, CancellationToken cancellationToken);

    /// <summary>Revierte el consumo (compensacion): devuelve la cotizacion a Active y des-redime el cupon.</summary>
    Task<PricingOperationResult> RevertQuoteAsync(Guid quoteId, Guid orderId, string reason, CancellationToken cancellationToken);
}

/// <summary>Resultado de una operacion de Pricing (consume/revert). ServiceAvailable=false indica que gRPC no respondio.</summary>
public sealed record PricingOperationResult(bool ServiceAvailable, bool Success, string ErrorCode, string Status)
{
    /// <summary>Resultado cuando el servicio Pricing no esta disponible (la saga reintentara).</summary>
    public static readonly PricingOperationResult Unavailable = new(ServiceAvailable: false, Success: false, "Pricing.Unavailable", string.Empty);
}

/// <summary>Snapshot de una cotizacion para construir el pedido.</summary>
public sealed record QuoteSnapshot(
    bool ServiceAvailable,
    bool Found,
    string Status,
    Guid CustomerUserId,
    Guid MerchantId,
    string Vertical,
    string Currency,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal ServiceFee,
    decimal Tax,
    decimal Tip,
    decimal DiscountAmount,
    decimal Total,
    IReadOnlyList<QuoteSnapshotLine> Lines)
{
    /// <summary>Snapshot cuando el servicio Pricing no esta disponible.</summary>
    public static readonly QuoteSnapshot Unavailable = new(
        ServiceAvailable: false, Found: false, string.Empty, Guid.Empty, Guid.Empty, string.Empty, string.Empty,
        0m, 0m, 0m, 0m, 0m, 0m, 0m, []);
}

/// <summary>Linea del snapshot de la cotizacion.</summary>
public sealed record QuoteSnapshotLine(Guid ItemId, string ItemName, decimal UnitPrice, decimal ModifierTotal, int Quantity);
