using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NpgsqlTypes;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo EF Core del agregado <see cref="Item"/>: Money (OwnsOne), atributos JSONB y full-text.</summary>
internal sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();

        builder.Property(item => item.MerchantId).IsRequired();
        builder.HasIndex(item => item.MerchantId);

        builder.Property(item => item.CategoryId);
        builder.HasIndex(item => item.CategoryId);

        builder.Property(item => item.Name).HasMaxLength(Item.MaxNameLength).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(Item.MaxDescriptionLength);

        builder.OwnsOne(item => item.Price, price =>
        {
            price.Property(money => money.Amount).HasColumnName("price_amount").HasColumnType("numeric(12,2)").IsRequired();
            price.Property(money => money.Currency).HasColumnName("price_currency").HasMaxLength(3).IsRequired();
        });
        builder.Navigation(item => item.Price).IsRequired();

        builder.Property(item => item.PhotoObjectKey).HasMaxLength(512);
        builder.Property(item => item.IsAvailable).IsRequired();
        builder.Property(item => item.TracksInventory).IsRequired();
        builder.Property(item => item.IsDeleted).IsRequired();
        builder.Property(item => item.CreatedAtUtc).IsRequired();

        // Atributos por vertical en JSONB: string serializado en columna jsonb. El comparador habilita
        // el change-tracking de la coleccion (ver gotcha de Fase 3).
        builder.Ignore(item => item.Attributes);
        Microsoft.EntityFrameworkCore.Metadata.IMutableProperty attributes =
            builder.Property<Dictionary<string, string>>("_attributes")
                .HasColumnName("attributes")
                .HasColumnType("jsonb")
                .IsRequired()
                .Metadata;
        attributes.SetValueConverter(AttributesConverter);
        attributes.SetValueComparer(AttributesComparer);

        // Vector de busqueda full-text (español) como columna generada + indice GIN.
        builder.Property<NpgsqlTsVector>("SearchVector")
            .HasColumnName("search_vector")
            .HasColumnType("tsvector")
            .HasComputedColumnSql(
                "to_tsvector('spanish', coalesce(\"Name\", '') || ' ' || coalesce(\"Description\", ''))",
                stored: true);
        builder.HasIndex("SearchVector").HasMethod("gin");

        builder.HasMany(item => item.Modifiers)
            .WithOne()
            .HasForeignKey(modifier => modifier.ItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(Item.Modifiers))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasQueryFilter(item => !item.IsDeleted);
    }

    private static readonly JsonSerializerOptions AttributesJsonOptions = new(JsonSerializerDefaults.General);

    private static readonly ValueConverter<Dictionary<string, string>, string> AttributesConverter =
        new(
            attributes => JsonSerializer.Serialize(attributes, AttributesJsonOptions),
            json => Deserialize(json));

    private static readonly ValueComparer<Dictionary<string, string>> AttributesComparer =
        new(
            (left, right) => Compare(left, right),
            attributes => attributes.Aggregate(
                0,
                (hash, pair) => HashCode.Combine(
                    hash,
                    pair.Key.GetHashCode(StringComparison.OrdinalIgnoreCase),
                    pair.Value.GetHashCode(StringComparison.Ordinal))),
            attributes => new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase));

    private static Dictionary<string, string> Deserialize(string json) =>
        new(
            JsonSerializer.Deserialize<Dictionary<string, string>>(json, AttributesJsonOptions) ?? [],
            StringComparer.OrdinalIgnoreCase);

    private static bool Compare(Dictionary<string, string>? left, Dictionary<string, string>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, string> pair in left)
        {
            if (!right.TryGetValue(pair.Key, out string? value) || !string.Equals(value, pair.Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
