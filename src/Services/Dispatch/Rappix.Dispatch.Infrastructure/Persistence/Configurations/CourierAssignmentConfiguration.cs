using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core de <see cref="CourierAssignment"/>. La invariante "un courier solo tiene una
/// asignacion activa" y "un pedido solo tiene una asignacion activa" la garantizan dos UNIQUE
/// PARTIAL indexes (WHERE ReleasedAtUtc IS NULL) — red de seguridad ante carreras concurrentes
/// si el codigo de aplicacion se rompe en el futuro.
/// </summary>
internal sealed class CourierAssignmentConfiguration : IEntityTypeConfiguration<CourierAssignment>
{
    // Converter compartido (lista de lineas <-> JSON string) — Fase 13.6. System.Text.Json.JsonSerializer
    // default es suficiente (record con 2 props, sin polimorfismo).
    private static readonly ValueConverter<IReadOnlyList<AssignmentLineSnapshot>, string> LinesConverter = new(
        list => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null),
        json => string.IsNullOrEmpty(json)
            ? new List<AssignmentLineSnapshot>()
            : (JsonSerializer.Deserialize<List<AssignmentLineSnapshot>>(json, (JsonSerializerOptions?)null) ?? new List<AssignmentLineSnapshot>()));

    // Comparator para detectar cambios en la coleccion (sin esto EF no marca el snapshot dirty si solo
    // cambia el contenido de la lista). Patron espejo de Catalog._attributes.
    private static readonly ValueComparer<IReadOnlyList<AssignmentLineSnapshot>> LinesComparer = new(
        (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
        list => list == null ? 0 : list.Aggregate(0, (acc, line) => HashCode.Combine(acc, line.ItemName, line.Quantity)),
        list => (IReadOnlyList<AssignmentLineSnapshot>)list.ToList());

    public void Configure(EntityTypeBuilder<CourierAssignment> builder)
    {
        builder.ToTable("courier_assignments");

        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Id).ValueGeneratedNever();

        // FK logico al aggregate CourierProfile (sin restriccion para evitar acoplar el delete del
        // profile a su historial). El converter de CourierId se aplica por convencion.
        builder.Property(assignment => assignment.CourierId).IsRequired();

        // OrderId vive en otro servicio: sin FK.
        builder.Property(assignment => assignment.OrderId).IsRequired();

        builder.Property(assignment => assignment.AssignedAtUtc).IsRequired();
        builder.Property(assignment => assignment.ReleasedAtUtc);
        builder.Property(assignment => assignment.ReleaseReason).HasMaxLength(50);

        // Snapshot del pedido (Fase 13.6). OwnsOne con prefijo snapshot_* para los escalares; las
        // lineas como JSONB (patron Catalog._attributes). El navigation IsRequired para que EF lo
        // materialice siempre — filas pre-13.6 traen los defaults de la migration (uuid ceros, '', 0).
        builder.OwnsOne(assignment => assignment.Snapshot, snap =>
        {
            snap.Property(s => s.CustomerUserId).HasColumnName("snapshot_customer_user_id").IsRequired();
            snap.Property(s => s.MerchantName).HasColumnName("snapshot_merchant_name").HasMaxLength(200).IsRequired();
            snap.Property(s => s.PickupLatitude).HasColumnName("snapshot_pickup_lat").IsRequired();
            snap.Property(s => s.PickupLongitude).HasColumnName("snapshot_pickup_lng").IsRequired();
            snap.Property(s => s.DeliveryStreet).HasColumnName("snapshot_delivery_street").HasMaxLength(300).IsRequired();
            snap.Property(s => s.DeliveryReference).HasColumnName("snapshot_delivery_reference").HasMaxLength(300);
            snap.Property(s => s.DeliveryLatitude).HasColumnName("snapshot_delivery_lat").IsRequired();
            snap.Property(s => s.DeliveryLongitude).HasColumnName("snapshot_delivery_lng").IsRequired();
            snap.Property(s => s.OrderTotal).HasColumnName("snapshot_order_total").HasColumnType("numeric(19,4)").IsRequired();
            snap.Property(s => s.OrderCurrency).HasColumnName("snapshot_order_currency").HasMaxLength(3).IsRequired();
            // Lines como JSONB serializado; el value comparer cubre el change-tracking.
            snap.Property(s => s.Lines)
                .HasColumnName("snapshot_lines")
                .HasColumnType("jsonb")
                .HasConversion(LinesConverter, LinesComparer)
                .IsRequired();
        });
        builder.Navigation(assignment => assignment.Snapshot).IsRequired();

        // Unique partial indexes — la red de seguridad ante carreras del claim.
        builder.HasIndex(assignment => assignment.CourierId)
            .HasFilter("\"ReleasedAtUtc\" IS NULL")
            .IsUnique()
            .HasDatabaseName("ux_courier_assignments_active_courier");

        builder.HasIndex(assignment => assignment.OrderId)
            .HasFilter("\"ReleasedAtUtc\" IS NULL")
            .IsUnique()
            .HasDatabaseName("ux_courier_assignments_active_order");

        // Historial por courier (lookup en orden cronologico descendente).
        builder.HasIndex(assignment => new { assignment.CourierId, assignment.AssignedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("ix_courier_assignments_courier_history");

        // Concurrencia optimista (poco usado en esta entidad pero consistente con el resto).
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
