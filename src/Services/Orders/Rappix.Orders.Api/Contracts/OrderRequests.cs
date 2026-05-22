namespace Rappix.Orders.Api.Contracts;

/// <summary>Cuerpo para crear un pedido: la cotizacion congelada y la direccion de entrega.</summary>
public sealed record PlaceOrderRequest(Guid QuoteId, string Street, string? Reference, double Latitude, double Longitude);

/// <summary>Cuerpo opcional para cancelar un pedido.</summary>
public sealed record CancelOrderRequest(string? Reason);

/// <summary>Cuerpo para que el merchant rechace un pedido.</summary>
public sealed record RejectOrderRequest(string? Reason);
