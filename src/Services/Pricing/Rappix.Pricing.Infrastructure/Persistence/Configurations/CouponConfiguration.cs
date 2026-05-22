using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Pricing.Domain.Coupons;

namespace Rappix.Pricing.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core del agregado <see cref="Coupon"/>. El codigo es unico (los codigos borrados quedan
/// reservados). UsedCount usa el token xmin de PostgreSQL como concurrencia optimista para no exceder
/// MaxUses ante consumos concurrentes. Borrado logico via query filter.
/// </summary>
internal sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("coupons");

        builder.HasKey(coupon => coupon.Id);
        builder.Property(coupon => coupon.Id).ValueGeneratedNever();

        builder.Property(coupon => coupon.Code)
            .HasConversion(code => code.Value, value => CouponCode.FromTrusted(value))
            .HasColumnName("code")
            .HasMaxLength(CouponCode.MaxLength)
            .IsRequired();
        builder.HasIndex(coupon => coupon.Code).IsUnique();

        builder.Property(coupon => coupon.DiscountType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(coupon => coupon.Value).IsRequired();
        builder.Property(coupon => coupon.MaxUses);
        builder.Property(coupon => coupon.UsedCount).IsRequired();
        builder.Property(coupon => coupon.MinOrderAmount);
        builder.Property(coupon => coupon.ValidFromUtc).IsRequired();
        builder.Property(coupon => coupon.ValidUntilUtc).IsRequired();
        builder.Property(coupon => coupon.PerUserLimit);
        builder.Property(coupon => coupon.MerchantId);
        builder.HasIndex(coupon => coupon.MerchantId);
        builder.Property(coupon => coupon.IsActive).IsRequired();
        builder.Property(coupon => coupon.IsDeleted).IsRequired();
        builder.Property(coupon => coupon.CreatedAtUtc).IsRequired();
        builder.Property(coupon => coupon.UpdatedAtUtc);

        // Concurrencia optimista via la columna de sistema xmin de PostgreSQL (token de version de fila).
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasQueryFilter(coupon => !coupon.IsDeleted);
    }
}
