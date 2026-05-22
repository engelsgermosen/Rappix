using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Pricing.Domain.Common;
using Rappix.Pricing.Domain.Surge;

namespace Rappix.Pricing.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo EF Core del agregado <see cref="SurgeRule"/>. Zona y vertical opcionales; borrado logico.</summary>
internal sealed class SurgeRuleConfiguration : IEntityTypeConfiguration<SurgeRule>
{
    public void Configure(EntityTypeBuilder<SurgeRule> builder)
    {
        builder.ToTable("surge_rules");

        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Id).ValueGeneratedNever();

        builder.Property(rule => rule.ZoneId).HasMaxLength(100);
        builder.HasIndex(rule => rule.ZoneId);

        builder.Property(rule => rule.Vertical)
            .HasConversion(
                vertical => vertical == null ? null : vertical.Value.ToString(),
                value => string.IsNullOrEmpty(value) ? null : Enum.Parse<VerticalType>(value))
            .HasMaxLength(20);

        builder.Property(rule => rule.StartHour).IsRequired();
        builder.Property(rule => rule.EndHour).IsRequired();
        builder.Property(rule => rule.Multiplier).IsRequired();
        builder.Property(rule => rule.Priority).IsRequired();
        builder.Property(rule => rule.IsActive).IsRequired();
        builder.Property(rule => rule.IsDeleted).IsRequired();
        builder.Property(rule => rule.CreatedAtUtc).IsRequired();
        builder.Property(rule => rule.UpdatedAtUtc);

        builder.HasQueryFilter(rule => !rule.IsDeleted);
    }
}
