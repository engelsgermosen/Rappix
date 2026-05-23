using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core del agregado <see cref="CourierProfile"/>. Vehicle y LastLocation son value objects
/// OwnsOne (mismas filas, columnas con prefijo). Status como texto. xmin shadow para concurrencia
/// optimista en updates de perfil/vehiculo/location (no en el claim — ese usa WHERE Status='Online').
/// </summary>
internal sealed class CourierProfileConfiguration : IEntityTypeConfiguration<CourierProfile>
{
    public void Configure(EntityTypeBuilder<CourierProfile> builder)
    {
        builder.ToTable("courier_profiles");

        builder.HasKey(courier => courier.Id);
        // CourierId = UserId de Identity; no se genera en BD.
        builder.Property(courier => courier.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(courier => courier.FirstName).HasMaxLength(100).IsRequired();

        builder.Property(courier => courier.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        // Indice para el filtro de seguridad post-GEOSEARCH (WHERE Id IN (...) AND Status='Online').
        builder.HasIndex(courier => courier.Status);

        builder.Property(courier => courier.CreatedAtUtc).IsRequired();
        builder.Property(courier => courier.UpdatedAtUtc);

        // Value object Vehicle (mismas filas; null hasta PUT /me/vehicle).
        builder.OwnsOne(courier => courier.Vehicle, vehicle =>
        {
            vehicle.Property(value => value.Type)
                .HasColumnName("vehicle_type")
                .HasConversion<string>()
                .HasMaxLength(10);
            vehicle.Property(value => value.Plate)
                .HasColumnName("vehicle_plate")
                .HasMaxLength(Vehicle.MaxPlateLength);
            vehicle.Property(value => value.CapacityKg)
                .HasColumnName("vehicle_capacity_kg")
                .HasColumnType("numeric(6,2)");
        });

        // Value object LastLocation (null hasta POST /me/location).
        builder.OwnsOne(courier => courier.LastLocation, location =>
        {
            location.Property(value => value.Latitude).HasColumnName("last_location_lat");
            location.Property(value => value.Longitude).HasColumnName("last_location_lng");
            location.Property(value => value.ReportedAtUtc).HasColumnName("last_location_at_utc");
        });

        // Concurrencia optimista via la columna de sistema xmin de PostgreSQL.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
