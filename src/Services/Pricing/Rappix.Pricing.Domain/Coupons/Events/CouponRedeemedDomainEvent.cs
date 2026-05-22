using Rappix.BuildingBlocks.Core.Domain;
using Rappix.Pricing.Domain.Quotes;

namespace Rappix.Pricing.Domain.Coupons.Events;

/// <summary>Se eleva cuando un cupon se redime al consumir una cotizacion. Se traduce a evento de integracion.</summary>
public sealed record CouponRedeemedDomainEvent(
    CouponId CouponId,
    string Code,
    Guid CustomerUserId,
    QuoteId QuoteId,
    decimal DiscountAmount) : DomainEvent;
