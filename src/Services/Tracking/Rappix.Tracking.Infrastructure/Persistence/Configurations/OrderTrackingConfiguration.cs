using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Tracking.Domain.OrderTrackings;

namespace Rappix.Tracking.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core del read model <see cref="OrderTracking"/>. PK = OrderId (Guid plano, no se
/// genera en BD — lo trae el evento OrderSubmitted). Status como texto (legibilidad en BD). xmin
/// shadow para concurrencia optimista.
/// </summary>
internal sealed class OrderTrackingConfiguration : IEntityTypeConfiguration<OrderTracking>
{
    public void Configure(EntityTypeBuilder<OrderTracking> builder)
    {
        builder.ToTable("order_trackings");

        builder.HasKey(tracking => tracking.Id);
        builder.Property(tracking => tracking.Id).ValueGeneratedNever();

        builder.Property(tracking => tracking.CustomerUserId).IsRequired();
        builder.Property(tracking => tracking.MerchantId).IsRequired();

        builder.Property(tracking => tracking.CurrentStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(tracking => tracking.StatusReason).HasMaxLength(500);

        builder.Property(tracking => tracking.LastCourierId);
        builder.Property(tracking => tracking.LastCourierLat);
        builder.Property(tracking => tracking.LastCourierLng);
        builder.Property(tracking => tracking.LastLocationAtUtc);

        builder.Property(tracking => tracking.PickupLat).IsRequired();
        builder.Property(tracking => tracking.PickupLng).IsRequired();
        builder.Property(tracking => tracking.DeliveryLat).IsRequired();
        builder.Property(tracking => tracking.DeliveryLng).IsRequired();

        builder.Property(tracking => tracking.CreatedAtUtc).IsRequired();
        builder.Property(tracking => tracking.UpdatedAtUtc).IsRequired();

        // Indice por cliente: soporta el filtro "mis pedidos en tracking activo" (follow-up) y
        // los eventuales reportes por usuario sin escaneo completo.
        builder.HasIndex(tracking => tracking.CustomerUserId);

        // Indice por (status, updated_at_utc): habilita housekeeping futuro (purgar entregados +30 dias).
        builder.HasIndex(tracking => new { tracking.CurrentStatus, tracking.UpdatedAtUtc });

        // Concurrencia optimista via la columna de sistema xmin de PostgreSQL.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
