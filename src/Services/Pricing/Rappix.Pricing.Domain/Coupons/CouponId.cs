namespace Rappix.Pricing.Domain.Coupons;

/// <summary>Identificador fuertemente tipado de un cupon (UUIDv7).</summary>
public readonly record struct CouponId(Guid Value)
{
    /// <summary>Genera un nuevo identificador basado en UUIDv7.</summary>
    public static CouponId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
