using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core de ServiceArea. Discriminada por Type: poligono (geometry) o circulo
/// (geography + radio). Un CHECK garantiza que exactamente una forma este presente, e indices
/// GiST aceleran las consultas espaciales.
/// </summary>
internal sealed class ServiceAreaConfiguration : IEntityTypeConfiguration<ServiceArea>
{
    public void Configure(EntityTypeBuilder<ServiceArea> builder)
    {
        builder.ToTable("service_areas", table => table.HasCheckConstraint(
            "ck_service_areas_shape",
            """
            ("Type" = 'Polygon' AND "Polygon" IS NOT NULL AND "Center" IS NULL AND "RadiusMeters" IS NULL)
            OR ("Type" = 'Circle' AND "Center" IS NOT NULL AND "RadiusMeters" IS NOT NULL AND "Polygon" IS NULL)
            """));

        builder.HasKey(area => area.Id);
        builder.Property(area => area.Id).ValueGeneratedNever();

        // La conversion de MerchantId (FK) se aplica por convencion (ConfigureConventions).
        builder.Property(area => area.MerchantId).IsRequired();

        builder.Property(area => area.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(area => area.Polygon).HasColumnType("geometry(Polygon,4326)");
        builder.Property(area => area.Center).HasColumnType("geography(Point,4326)");
        builder.Property(area => area.RadiusMeters);
        builder.Property(area => area.CreatedAtUtc).IsRequired();

        builder.HasIndex(area => area.MerchantId);
        builder.HasIndex(area => area.Polygon).HasMethod("gist");
        builder.HasIndex(area => area.Center).HasMethod("gist");
    }
}
