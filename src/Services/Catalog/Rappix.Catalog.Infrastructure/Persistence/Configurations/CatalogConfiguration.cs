using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Catalog.Domain.Catalogs;

namespace Rappix.Catalog.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo EF Core del agregado <see cref="MerchantCatalog"/>.</summary>
internal sealed class CatalogConfiguration : IEntityTypeConfiguration<MerchantCatalog>
{
    public void Configure(EntityTypeBuilder<MerchantCatalog> builder)
    {
        builder.ToTable("catalogs");
        builder.HasKey(catalog => catalog.Id);
        builder.Property(catalog => catalog.Id).ValueGeneratedNever();

        builder.Property(catalog => catalog.MerchantId).IsRequired();
        builder.HasIndex(catalog => catalog.MerchantId).IsUnique();

        builder.Property(catalog => catalog.VerticalType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(catalog => catalog.IsEnabled).IsRequired();
        builder.Property(catalog => catalog.CreatedAtUtc).IsRequired();

        builder.HasMany(catalog => catalog.Categories)
            .WithOne()
            .HasForeignKey(category => category.CatalogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(MerchantCatalog.Categories))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
