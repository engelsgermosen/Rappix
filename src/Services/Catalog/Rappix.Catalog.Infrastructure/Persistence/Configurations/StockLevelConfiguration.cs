using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core del agregado <see cref="StockLevel"/>. PK = ItemId (1:1 con el item). Usa el
/// sistema xmin de PostgreSQL como token de concurrencia optimista para evitar la sobreventa.
/// </summary>
internal sealed class StockLevelConfiguration : IEntityTypeConfiguration<StockLevel>
{
    public void Configure(EntityTypeBuilder<StockLevel> builder)
    {
        builder.ToTable("stock_levels");

        // La PK es el ItemId (se convierte por convencion). No se genera (igual al item).
        builder.HasKey(stock => stock.Id);
        builder.Property(stock => stock.Id).HasColumnName("item_id").ValueGeneratedNever();

        builder.Property(stock => stock.MerchantId).IsRequired();
        builder.HasIndex(stock => stock.MerchantId);

        builder.Property(stock => stock.Quantity).IsRequired();
        builder.Property(stock => stock.UpdatedAtUtc).IsRequired();

        // Concurrencia optimista via la columna de sistema xmin de PostgreSQL (token de version de fila).
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
