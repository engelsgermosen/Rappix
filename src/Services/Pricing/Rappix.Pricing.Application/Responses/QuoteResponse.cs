using Rappix.Pricing.Domain.Quotes;

namespace Rappix.Pricing.Application.Responses;

/// <summary>Respuesta de una cotizacion con su desglose completo.</summary>
public sealed record QuoteResponse(
    Guid QuoteId,
    Guid CustomerUserId,
    Guid MerchantId,
    string Vertical,
    string Currency,
    IReadOnlyList<QuoteLineResponse> Lines,
    PriceBreakdownResponse Breakdown,
    string? CouponCode,
    string Status,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? ConsumedAtUtc)
{
    /// <summary>Proyecta el agregado cotizacion a su DTO.</summary>
    public static QuoteResponse From(Quote quote) =>
        new(
            quote.Id.Value,
            quote.CustomerUserId,
            quote.MerchantId,
            quote.Vertical.ToString(),
            quote.Currency,
            [.. quote.Lines.Select(QuoteLineResponse.From)],
            PriceBreakdownResponse.From(quote.Breakdown),
            quote.AppliedCouponCode,
            quote.Status.ToString(),
            quote.CreatedAtUtc,
            quote.ExpiresAtUtc,
            quote.ConsumedAtUtc);
}

/// <summary>Linea de una cotizacion.</summary>
public sealed record QuoteLineResponse(
    Guid ItemId,
    string ItemName,
    decimal UnitPrice,
    decimal ModifierTotal,
    int Quantity,
    decimal LineSubtotal)
{
    /// <summary>Proyecta una linea a su DTO.</summary>
    public static QuoteLineResponse From(QuoteLine line) =>
        new(line.ItemId, line.ItemName, line.UnitPrice, line.ModifierTotal, line.Quantity, line.LineSubtotal);
}

/// <summary>Desglose monetario de una cotizacion (cada componente visible para transparencia).</summary>
public sealed record PriceBreakdownResponse(
    decimal Subtotal,
    decimal SurgeMultiplier,
    decimal SurgeAmount,
    decimal DiscountAmount,
    decimal DeliveryFee,
    decimal ServiceFee,
    decimal Tax,
    decimal Tip,
    decimal Total)
{
    /// <summary>Proyecta el desglose a su DTO.</summary>
    public static PriceBreakdownResponse From(PriceBreakdown breakdown) =>
        new(
            breakdown.Subtotal,
            breakdown.SurgeMultiplier,
            breakdown.SurgeAmount,
            breakdown.DiscountAmount,
            breakdown.DeliveryFee,
            breakdown.ServiceFee,
            breakdown.Tax,
            breakdown.Tip,
            breakdown.Total);
}
