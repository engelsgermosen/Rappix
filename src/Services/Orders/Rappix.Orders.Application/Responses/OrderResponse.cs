using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Application.Responses;

/// <summary>Respuesta de un pedido con su snapshot completo y estado actual.</summary>
public sealed record OrderResponse(
    Guid OrderId,
    Guid CustomerUserId,
    Guid MerchantId,
    Guid MerchantOwnerUserId,
    Guid QuoteId,
    string Vertical,
    string Currency,
    string Status,
    IReadOnlyList<OrderLineResponse> Lines,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal ServiceFee,
    decimal Tax,
    decimal Tip,
    decimal DiscountAmount,
    decimal TotalAmount,
    DeliveryAddressResponse DeliveryAddress,
    string? CancellationReason,
    DateTime CreatedAtUtc,
    DateTime? AcceptedAtUtc,
    DateTime? InProgressAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? ClosedAtUtc)
{
    /// <summary>Proyecta el agregado pedido a su DTO.</summary>
    public static OrderResponse From(Order order) =>
        new(
            order.Id.Value,
            order.CustomerUserId,
            order.MerchantId,
            order.MerchantOwnerUserId,
            order.QuoteId,
            order.Vertical,
            order.Currency,
            order.Status.ToString(),
            [.. order.Lines.Select(OrderLineResponse.From)],
            order.Subtotal,
            order.DeliveryFee,
            order.ServiceFee,
            order.Tax,
            order.Tip,
            order.DiscountAmount,
            order.TotalAmount,
            new DeliveryAddressResponse(order.DeliveryAddress.Street, order.DeliveryAddress.Reference, order.DeliveryAddress.Latitude, order.DeliveryAddress.Longitude),
            order.CancellationReason,
            order.CreatedAtUtc,
            order.AcceptedAtUtc,
            order.InProgressAtUtc,
            order.CompletedAtUtc,
            order.ClosedAtUtc);
}

/// <summary>Linea del pedido (snapshot).</summary>
public sealed record OrderLineResponse(Guid ItemId, string ItemName, decimal UnitPrice, decimal ModifierTotal, int Quantity, decimal LineSubtotal)
{
    /// <summary>Proyecta una linea a su DTO.</summary>
    public static OrderLineResponse From(OrderLine line) =>
        new(line.ItemId, line.ItemName, line.UnitPrice, line.ModifierTotal, line.Quantity, line.LineSubtotal);
}

/// <summary>Direccion de entrega.</summary>
public sealed record DeliveryAddressResponse(string Street, string? Reference, double Latitude, double Longitude);
