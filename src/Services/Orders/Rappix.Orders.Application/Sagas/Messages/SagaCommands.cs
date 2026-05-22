namespace Rappix.Orders.Application.Sagas.Messages;

// Comandos internos que la saga ENVIA a los consumers "activity" (que hacen el efecto gRPC y publican
// el evento-resultado de vuelta). Todos se correlacionan por OrderId. No cruzan el contrato de otros
// servicios: son la plomeria interna de la saga de Orders.

/// <summary>Pide consumir la cotizacion (congelar precio, redimir cupon).</summary>
public sealed record ConsumeQuote(Guid OrderId, Guid QuoteId);

/// <summary>Pide reservar (hold) el stock del pedido. El activity carga las lineas del pedido.</summary>
public sealed record ReserveStock(Guid OrderId, int TtlSeconds);

/// <summary>Pide confirmar (decremento definitivo) el stock reservado del pedido.</summary>
public sealed record CommitStock(Guid OrderId);

/// <summary>Pide liberar el stock reservado del pedido (compensacion).</summary>
public sealed record ReleaseStock(Guid OrderId);

/// <summary>Pide revertir el consumo de la cotizacion (compensacion).</summary>
public sealed record RevertQuote(Guid OrderId, Guid QuoteId, string Reason);
