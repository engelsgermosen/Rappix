using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo EF Core de OperatingHours (un rango horario por fila). DayOfWeek se guarda como entero.</summary>
internal sealed class OperatingHoursConfiguration : IEntityTypeConfiguration<OperatingHours>
{
    public void Configure(EntityTypeBuilder<OperatingHours> builder)
    {
        builder.ToTable("operating_hours");

        builder.HasKey(hours => hours.Id);
        builder.Property(hours => hours.Id).ValueGeneratedNever();

        // La conversion de MerchantId (FK) se aplica por convencion (ConfigureConventions).
        builder.Property(hours => hours.MerchantId).IsRequired();

        builder.Property(hours => hours.DayOfWeek).IsRequired();
        builder.Property(hours => hours.OpensAt).IsRequired();
        builder.Property(hours => hours.ClosesAt).IsRequired();

        builder.HasIndex(hours => hours.MerchantId);
    }
}
