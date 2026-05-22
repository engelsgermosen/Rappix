using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Pricing.Domain.ItemPrices;

namespace Rappix.Pricing.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo EF Core de <see cref="ItemPriceCache"/> (cache local de precios alimentado por eventos de Catalog). PK = ItemId.</summary>
internal sealed class ItemPriceCacheConfiguration : IEntityTypeConfiguration<ItemPriceCache>
{
    public void Configure(EntityTypeBuilder<ItemPriceCache> builder)
    {
        builder.ToTable("item_price_cache");

        builder.HasKey(cache => cache.ItemId);
        builder.Property(cache => cache.ItemId).ValueGeneratedNever();

        builder.Property(cache => cache.MerchantId).IsRequired();
        builder.HasIndex(cache => cache.MerchantId);

        builder.Property(cache => cache.Name).HasMaxLength(200).IsRequired();
        builder.Property(cache => cache.BasePrice).IsRequired();
        builder.Property(cache => cache.Currency).HasMaxLength(3).IsRequired();
        builder.Property(cache => cache.UpdatedAtUtc).IsRequired();
    }
}
