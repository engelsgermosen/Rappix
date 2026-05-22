using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Infrastructure.Persistence.Converters;

/// <summary>Convierte el identificador fuertemente tipado ItemId a Guid para la persistencia.</summary>
internal sealed class ItemIdConverter()
    : ValueConverter<ItemId, Guid>(id => id.Value, value => new ItemId(value));
