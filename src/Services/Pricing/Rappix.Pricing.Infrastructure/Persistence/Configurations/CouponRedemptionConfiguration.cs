using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Pricing.Domain.Coupons;

namespace Rappix.Pricing.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo EF Core de <see cref="CouponRedemption"/> (registro de uso por cliente, para el limite por usuario).</summary>
internal sealed class CouponRedemptionConfiguration : IEntityTypeConfiguration<CouponRedemption>
{
    public void Configure(EntityTypeBuilder<CouponRedemption> builder)
    {
        builder.ToTable("coupon_redemptions");

        builder.HasKey(redemption => redemption.Id);
        builder.Property(redemption => redemption.Id).ValueGeneratedNever();

        builder.Property(redemption => redemption.CouponId).IsRequired();
        builder.Property(redemption => redemption.CustomerUserId).IsRequired();
        builder.Property(redemption => redemption.QuoteId).IsRequired();
        builder.Property(redemption => redemption.RedeemedAtUtc).IsRequired();

        // Reversion por compensacion de la saga (auditoria: no se borra el registro, se marca revertido).
        builder.Property(redemption => redemption.RevertedAtUtc);
        builder.Property(redemption => redemption.RevertReason).HasMaxLength(200);

        // Indice para contar redenciones de un (cupon, cliente) al validar el limite por usuario.
        builder.HasIndex(redemption => new { redemption.CouponId, redemption.CustomerUserId });

        // Indice para localizar la redencion activa de una cotizacion al revertir.
        builder.HasIndex(redemption => redemption.QuoteId);
    }
}
