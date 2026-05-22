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

    /// <summary>Momento en que la redencion fue revertida por compensacion de la saga (UTC), si lo fue.</summary>
    public DateTime? RevertedAtUtc { get; private set; }

    /// <summary>Razon de la reversion (auditoria).</summary>
    public string? RevertReason { get; private set; }

    /// <summary>Indica si la redencion fue revertida (la auditoria se conserva: no se borra el registro).</summary>
    public bool IsReverted => RevertedAtUtc is not null;

    /// <summary>Crea un registro de redencion.</summary>
    public static CouponRedemption Create(CouponId couponId, Guid customerUserId, QuoteId quoteId, DateTime redeemedAtUtc) =>
        new(Guid.CreateVersion7(), couponId, customerUserId, quoteId, redeemedAtUtc);

    /// <summary>
    /// Marca la redencion como revertida (conserva el rastro de auditoria: consumido -> revertido, sin borrar).
    /// Idempotente: si ya estaba revertida, no cambia nada.
    /// </summary>
    public void MarkReverted(string reason, DateTime utcNow)
    {
        if (IsReverted)
        {
            return;
        }

        RevertedAtUtc = utcNow;
        RevertReason = reason;
    }
}
