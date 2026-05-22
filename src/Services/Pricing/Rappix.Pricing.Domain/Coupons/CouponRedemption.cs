using Rappix.BuildingBlocks.Core.Domain;
using Rappix.Pricing.Domain.Quotes;

namespace Rappix.Pricing.Domain.Coupons;

/// <summary>
/// Registro de una redencion de cupon por un cliente en una cotizacion. Permite hacer cumplir el limite
/// por usuario (PerUserLimit) contando las redenciones de un (cupon, cliente). Se crea al consumir.
/// </summary>
public sealed class CouponRedemption : Entity<Guid>
{
    private CouponRedemption()
    {
    }

    private CouponRedemption(Guid id, CouponId couponId, Guid customerUserId, QuoteId quoteId, DateTime redeemedAtUtc)
        : base(id)
    {
        CouponId = couponId;
        CustomerUserId = customerUserId;
        QuoteId = quoteId;
        RedeemedAtUtc = redeemedAtUtc;
    }

    /// <summary>Cupon redimido.</summary>
    public CouponId CouponId { get; private set; }

    /// <summary>Cliente que lo redimio.</summary>
    public Guid CustomerUserId { get; private set; }

    /// <summary>Cotizacion en la que se redimio.</summary>
    public QuoteId QuoteId { get; private set; }

    /// <summary>Momento de la redencion (UTC).</summary>
    public DateTime RedeemedAtUtc { get; private set; }

    /// <summary>Crea un registro de redencion.</summary>
    public static CouponRedemption Create(CouponId couponId, Guid customerUserId, QuoteId quoteId, DateTime redeemedAtUtc) =>
        new(Guid.CreateVersion7(), couponId, customerUserId, quoteId, redeemedAtUtc);
}
