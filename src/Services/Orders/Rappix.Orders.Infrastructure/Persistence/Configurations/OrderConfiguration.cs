using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core del agregado <see cref="Order"/>: estado y vertical como texto, direccion de entrega como
/// value object embebido (OwnsOne) y las lineas como entidades hijas. Los pedidos no se borran (auditoria).
/// </summary>
internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(order => order.CustomerUserId).IsRequired();
        builder.HasIndex(order => order.CustomerUserId);

        builder.Property(order => order.MerchantId).IsRequired();
        builder.Property(order => order.MerchantOwnerUserId).IsRequired();
        // La consulta de pendientes del merchant filtra por el dueno (sub del JWT) + estado.
        builder.HasIndex(order => new { order.MerchantOwnerUserId, order.Status });

        builder.Property(order => order.QuoteId).IsRequired();
        builder.Property(order => order.Vertical).HasMaxLength(20).IsRequired();
        builder.Property(order => order.Currency).HasMaxLength(3).IsRequired();

        builder.Property(order => order.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(order => order.Status);

        builder.Property(order => order.CancellationReason).HasMaxLength(300);

        builder.Property(order => order.CreatedAtUtc).IsRequired();
        builder.Property(order => order.AcceptedAtUtc);
        builder.Property(order => order.InProgressAtUtc);
        builder.Property(order => order.CompletedAtUtc);
        builder.Property(order => order.ClosedAtUtc);
        builder.Property(order => order.UpdatedAtUtc);

        // Direccion de entrega como value object embebido.
        builder.OwnsOne(order => order.DeliveryAddress, address =>
        {
            address.Property(value => value.Street).HasColumnName("delivery_street").HasMaxLength(300).IsRequired();
            address.Property(value => value.Reference).HasColumnName("delivery_reference").HasMaxLength(300);
            address.Property(value => value.Latitude).HasColumnName("delivery_latitude");
            address.Property(value => value.Longitude).HasColumnName("delivery_longitude");
        });
        builder.Navigation(order => order.DeliveryAddress).IsRequired();

        builder.HasMany(order => order.Lines)
            .WithOne()
            .HasForeignKey(line => line.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(Order.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
