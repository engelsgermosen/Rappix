namespace Rappix.Orders.Application.Sagas.Messages;

// Eventos-resultado que los consumers "activity" PUBLICAN de vuelta a la saga tras ejecutar el efecto gRPC.
// Se correlacionan por OrderId.

/// <summary>La cotizacion se consumio con exito.</summary>
public sealed record QuoteConsumed(Guid OrderId);

/// <summary>El consumo de la cotizacion fallo (expirada, ya consumida por otro, etc.).</summary>
public sealed record QuoteConsumptionFailed(Guid OrderId, string Reason);

/// <summary>El stock se reservo con exito.</summary>
public sealed record StockReserved(Guid OrderId);

/// <summary>La reserva de stock fallo (sin disponible suficiente).</summary>
public sealed record StockReservationFailed(Guid OrderId, string Reason);

/// <summary>El stock se confirmo (decremento definitivo) con exito.</summary>
public sealed record StockCommitted(Guid OrderId);

/// <summary>La confirmacion de stock fallo (excepcional: la reserva ya garantizaba el inventario).</summary>
public sealed record StockCommitFailed(Guid OrderId, string Reason);

/// <summary>El stock reservado se libero (compensacion completada).</summary>
public sealed record StockReleased(Guid OrderId);

/// <summary>El consumo de la cotizacion se revirtio (compensacion completada).</summary>
public sealed record QuoteReverted(Guid OrderId);

/// <summary>La reversion de la cotizacion fallo (se finaliza igual, best-effort).</summary>
public sealed record QuoteRevertFailed(Guid OrderId, string Reason);
