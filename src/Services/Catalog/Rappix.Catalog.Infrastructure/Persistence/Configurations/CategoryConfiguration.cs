using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Catalog.Domain.Catalogs;

namespace Rappix.Catalog.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo EF Core de la entidad hija <see cref="Category"/>.</summary>
internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).ValueGeneratedNever();

        // CatalogId se convierte por convencion (ConfigureConventions).
        builder.Property(category => category.Name).HasMaxLength(MerchantCatalog.MaxCategoryNameLength).IsRequired();
        builder.Property(category => category.SortOrder).IsRequired();

        builder.HasIndex(category => category.CatalogId);
    }
}
