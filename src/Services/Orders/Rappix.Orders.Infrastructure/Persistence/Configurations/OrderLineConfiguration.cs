using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo EF Core de <see cref="OrderLine"/> (snapshot inmutable de un item del pedido).</summary>
internal sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("order_lines");

        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).ValueGeneratedNever();

        builder.Property(line => line.OrderId).HasColumnName("order_id").IsRequired();
        builder.HasIndex(line => line.OrderId);

        builder.Property(line => line.ItemId).IsRequired();
        builder.Property(line => line.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(line => line.Quantity).IsRequired();
    }
}
