using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Pricing.Domain.Quotes;

namespace Rappix.Pricing.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core del agregado <see cref="Quote"/>: enums como texto, el desglose como value object embebido
/// (OwnsOne) y las lineas como entidades hijas. Las cotizaciones no se borran (auditoria); expiran.
/// </summary>
internal sealed class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("quotes");

        builder.HasKey(quote => quote.Id);
        builder.Property(quote => quote.Id).ValueGeneratedNever();

        builder.Property(quote => quote.CustomerUserId).IsRequired();
        builder.HasIndex(quote => quote.CustomerUserId);

        builder.Property(quote => quote.MerchantId).IsRequired();
        builder.HasIndex(quote => quote.MerchantId);

        builder.Property(quote => quote.Vertical).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(quote => quote.Currency).HasMaxLength(3).IsRequired();

        builder.Property(quote => quote.AppliedCouponId);
        builder.Property(quote => quote.AppliedCouponCode).HasMaxLength(40);

        builder.Property(quote => quote.CreatedAtUtc).IsRequired();
        builder.Property(quote => quote.ExpiresAtUtc).IsRequired();
        builder.HasIndex(quote => quote.ExpiresAtUtc);

        builder.Property(quote => quote.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(quote => quote.Status);

        builder.Property(quote => quote.ConsumedAtUtc);
        builder.Property(quote => quote.ConsumedByOrderId);
        builder.HasIndex(quote => quote.ConsumedByOrderId);
        builder.Property(quote => quote.RevertedAtUtc);

        // Desglose monetario como value object embebido (numeric(19,4) por convencion).
        builder.OwnsOne(quote => quote.Breakdown, breakdown =>
        {
            breakdown.Property(value => value.Subtotal).HasColumnName("subtotal");
            breakdown.Property(value => value.SurgeMultiplier).HasColumnName("surge_multiplier");
            breakdown.Property(value => value.SurgeAmount).HasColumnName("surge_amount");
            breakdown.Property(value => value.DiscountAmount).HasColumnName("discount_amount");
            breakdown.Property(value => value.DeliveryFee).HasColumnName("delivery_fee");
            breakdown.Property(value => value.ServiceFee).HasColumnName("service_fee");
            breakdown.Property(value => value.Tax).HasColumnName("tax");
            breakdown.Property(value => value.Tip).HasColumnName("tip");
            breakdown.Property(value => value.Total).HasColumnName("total");
        });
        builder.Navigation(quote => quote.Breakdown).IsRequired();

        builder.HasMany(quote => quote.Lines)
            .WithOne()
            .HasForeignKey(line => line.QuoteId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(Quote.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
