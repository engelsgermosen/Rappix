using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo EF Core del agregado Merchant.</summary>
internal sealed class MerchantConfiguration : IEntityTypeConfiguration<Merchant>
{
    public void Configure(EntityTypeBuilder<Merchant> builder)
    {
        builder.ToTable("merchants");
        builder.HasKey(merchant => merchant.Id);

        // La conversion de MerchantId se aplica por convencion (ConfigureConventions).
        builder.Property(merchant => merchant.Id).ValueGeneratedNever();

        builder.Property(merchant => merchant.OwnerUserId).IsRequired();
        builder.HasIndex(merchant => merchant.OwnerUserId).IsUnique();

        builder.Property(merchant => merchant.Name).HasMaxLength(150).IsRequired();

        builder.Property(merchant => merchant.Slug)
            .HasConversion(slug => slug.Value, value => Slug.FromTrusted(value))
            .HasMaxLength(60)
            .IsRequired();
        builder.HasIndex(merchant => merchant.Slug).IsUnique();

        // Tipado como ValueConverter base para aplicar un convertidor de Rnc (no nulo) a una
        // propiedad Rnc? (EF maneja el null). Postgres trata los NULL como distintos en el indice unico.
        ValueConverter rncConverter = new ValueConverter<Rnc, string>(
            rnc => rnc.Value,
            value => Rnc.FromTrusted(value));
        builder.Property(merchant => merchant.Rnc)
            .HasConversion(rncConverter)
            .HasMaxLength(11);
        builder.HasIndex(merchant => merchant.Rnc).IsUnique();

        builder.Property(merchant => merchant.Description).HasMaxLength(1000);

        builder.Property(merchant => merchant.VerticalType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(merchant => merchant.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(merchant => merchant.CommissionPercentage)
            .HasConversion(commission => commission.Value, value => CommissionPercentage.FromTrusted(value))
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(merchant => merchant.AverageRating).HasColumnType("numeric(3,2)");
        builder.Property(merchant => merchant.TotalReviews).IsRequired();
        builder.Property(merchant => merchant.LogoObjectKey).HasMaxLength(512);
        builder.Property(merchant => merchant.RejectionReason).HasMaxLength(500);
        builder.Property(merchant => merchant.SuspensionReason).HasMaxLength(500);
        builder.Property(merchant => merchant.IsDeleted).IsRequired();
        builder.Property(merchant => merchant.CreatedAtUtc).IsRequired();

        builder.HasMany(merchant => merchant.ServiceAreas)
            .WithOne()
            .HasForeignKey(area => area.MerchantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(merchant => merchant.OperatingHours)
            .WithOne()
            .HasForeignKey(hours => hours.MerchantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Merchant.ServiceAreas))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(Merchant.OperatingHours))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasQueryFilter(merchant => !merchant.IsDeleted);
    }
}
