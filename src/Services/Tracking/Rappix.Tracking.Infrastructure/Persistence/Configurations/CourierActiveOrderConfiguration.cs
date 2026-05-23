using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Tracking.Domain.CourierActiveOrders;

namespace Rappix.Tracking.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core de <see cref="CourierActiveOrder"/>. PK = CourierId (el lookup critico del consumer
/// de location es por courier). El unique index sobre <c>OrderId</c> refuerza el invariante 1↔1
/// desde el otro lado y habilita el DELETE por OrderId del consumer terminal.
/// </summary>
internal sealed class CourierActiveOrderConfiguration : IEntityTypeConfiguration<CourierActiveOrder>
{
    public void Configure(EntityTypeBuilder<CourierActiveOrder> builder)
    {
        builder.ToTable("courier_active_orders");

        builder.HasKey(map => map.Id);
        builder.Property(map => map.Id).ValueGeneratedNever();

        builder.Property(map => map.OrderId).IsRequired();
        builder.Property(map => map.AssignedAtUtc).IsRequired();

        builder.HasIndex(map => map.OrderId).IsUnique();

        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
