using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo EF Core de la entidad hija <see cref="Modifier"/> y sus opciones.</summary>
internal sealed class ModifierConfiguration : IEntityTypeConfiguration<Modifier>
{
    public void Configure(EntityTypeBuilder<Modifier> builder)
    {
        builder.ToTable("modifiers");
        builder.HasKey(modifier => modifier.Id);
        builder.Property(modifier => modifier.Id).ValueGeneratedNever();

        // ItemId se convierte por convencion (ConfigureConventions).
        builder.Property(modifier => modifier.Name).HasMaxLength(Modifier.MaxNameLength).IsRequired();
        builder.Property(modifier => modifier.IsRequired).IsRequired();
        builder.Property(modifier => modifier.MinSelections).IsRequired();
        builder.Property(modifier => modifier.MaxSelections).IsRequired();

        builder.HasIndex(modifier => modifier.ItemId);

        builder.HasMany(modifier => modifier.Options)
            .WithOne()
            .HasForeignKey(option => option.ModifierId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Modifier.Options))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
