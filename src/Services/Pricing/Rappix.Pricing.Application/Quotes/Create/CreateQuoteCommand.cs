using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Common;

namespace Rappix.Pricing.Application.Quotes.Create;

/// <summary>
/// Cotiza un carrito: resuelve precios (Catalog), surge, descuentos y envio, calcula el desglose y crea
/// una cotizacion persistida con expiracion.
/// </summary>
public sealed record CreateQuoteCommand(
    Guid CustomerUserId,
    Guid MerchantId,
    VerticalType Vertical,
    decimal DistanceKm,
    string? ZoneId,
    decimal Tip,
    string? CouponCode,
    bool IsFirstOrder,
    IReadOnlyList<QuoteLineInput> Lines) : IRequest<Result<QuoteResponse>>;

/// <summary>Linea solicitada del carrito.</summary>
/// <param name="ItemId">Item de Catalog.</param>
/// <param name="Quantity">Cantidad pedida (positiva).</param>
/// <param name="ModifierTotal">Suma de los modificadores seleccionados por unidad (no negativa).</param>
public sealed record QuoteLineInput(Guid ItemId, int Quantity, decimal ModifierTotal);
