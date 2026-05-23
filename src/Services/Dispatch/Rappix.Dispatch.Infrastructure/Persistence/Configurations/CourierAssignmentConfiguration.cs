using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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
