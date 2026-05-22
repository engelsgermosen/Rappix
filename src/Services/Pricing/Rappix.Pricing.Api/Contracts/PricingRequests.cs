using Rappix.Pricing.Domain.Common;
using Rappix.Pricing.Domain.Coupons;

namespace Rappix.Pricing.Api.Contracts;

/// <summary>Cuerpo para cotizar un carrito. El customerUserId se toma del JWT.</summary>
public sealed record CreateQuoteRequest(
    Guid MerchantId,
    VerticalType Vertical,
    decimal DistanceKm,
    string? ZoneId,
    decimal Tip,
    string? CouponCode,
    bool IsFirstOrder,
    IReadOnlyList<QuoteLineRequest> Lines);

/// <summary>Linea de un carrito a cotizar.</summary>
public sealed record QuoteLineRequest(Guid ItemId, int Quantity, decimal ModifierTotal);

/// <summary>Cuerpo para crear una regla de surge.</summary>
public sealed record CreateSurgeRuleRequest(
    string? ZoneId,
    VerticalType? Vertical,
    int StartHour,
    int EndHour,
    decimal Multiplier,
    int Priority);

/// <summary>Cuerpo para actualizar una regla de surge.</summary>
public sealed record UpdateSurgeRuleRequest(
    string? ZoneId,
    VerticalType? Vertical,
    int StartHour,
    int EndHour,
    decimal Multiplier,
    int Priority,
    bool IsActive);

/// <summary>Cuerpo para crear un cupon.</summary>
public sealed record CreateCouponRequest(
    string Code,
    DiscountType DiscountType,
    decimal Value,
    int? MaxUses,
    decimal? MinOrderAmount,
    DateTime ValidFromUtc,
    DateTime ValidUntilUtc,
    int? PerUserLimit,
    Guid? MerchantId);

/// <summary>Cuerpo para actualizar un cupon (el codigo es inmutable).</summary>
public sealed record UpdateCouponRequest(
    DiscountType DiscountType,
    decimal Value,
    int? MaxUses,
    decimal? MinOrderAmount,
    DateTime ValidFromUtc,
    DateTime ValidUntilUtc,
    int? PerUserLimit,
    Guid? MerchantId,
    bool IsActive);
