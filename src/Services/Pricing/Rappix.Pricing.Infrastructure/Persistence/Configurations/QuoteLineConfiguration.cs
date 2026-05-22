using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Pricing.Domain.Quotes;

namespace Rappix.Pricing.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo EF Core de la entidad hija <see cref="QuoteLine"/>.</summary>
internal sealed class QuoteLineConfiguration : IEntityTypeConfiguration<QuoteLine>
{
    public void Configure(EntityTypeBuilder<QuoteLine> builder)
    {
        builder.ToTable("quote_lines");

        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();

        builder.Property(line => line.QuoteId).IsRequired();
        builder.HasIndex(line => line.QuoteId);

        builder.Property(line => line.ItemId).IsRequired();
        builder.Property(line => line.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(line => line.UnitPrice).IsRequired();
        builder.Property(line => line.ModifierTotal).IsRequired();
        builder.Property(line => line.Quantity).IsRequired();
        builder.Property(line => line.LineSubtotal).IsRequired();
    }
}
