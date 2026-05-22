using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo EF Core de la entidad hija <see cref="ModifierOption"/>.</summary>
internal sealed class ModifierOptionConfiguration : IEntityTypeConfiguration<ModifierOption>
{
    public void Configure(EntityTypeBuilder<ModifierOption> builder)
    {
        builder.ToTable("modifier_options");
        builder.HasKey(option => option.Id);
        builder.Property(option => option.Id).ValueGeneratedNever();

        builder.Property(option => option.Name).HasMaxLength(80).IsRequired();
        builder.Property(option => option.PriceDelta).HasColumnType("numeric(12,2)").IsRequired();

        builder.HasIndex(option => option.ModifierId);
    }
}
