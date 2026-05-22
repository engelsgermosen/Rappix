using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core de <see cref="StockReservation"/>. Indice unico en (OrderId, ItemId): a lo sumo un
/// hold por pedido-item (ancla de idempotencia). Indice en (ItemId, Status) para el barrido de holds
/// vencidos. xmin como token de concurrencia optimista.
/// </summary>
internal sealed class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable("stock_reservations");

        builder.HasKey(reservation => reservation.Id);
        builder.Property(reservation => reservation.Id).ValueGeneratedNever();

        builder.Property(reservation => reservation.OrderId).IsRequired();
        builder.Property(reservation => reservation.ItemId).HasColumnName("item_id").IsRequired();
        builder.Property(reservation => reservation.MerchantId).IsRequired();
        builder.Property(reservation => reservation.Quantity).IsRequired();
        builder.Property(reservation => reservation.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(reservation => reservation.CreatedAtUtc).IsRequired();
        builder.Property(reservation => reservation.ExpiresAtUtc).IsRequired();
        builder.Property(reservation => reservation.UpdatedAtUtc);

        builder.HasIndex(reservation => reservation.OrderId);
        builder.HasIndex(reservation => new { reservation.OrderId, reservation.ItemId }).IsUnique();
        builder.HasIndex(reservation => new { reservation.ItemId, reservation.Status });

        // Concurrencia optimista via la columna de sistema xmin de PostgreSQL.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
