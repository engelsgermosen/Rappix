using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Rappix.Catalog.Domain.Catalogs;

namespace Rappix.Catalog.Infrastructure.Persistence.Converters;

/// <summary>Convierte el identificador fuertemente tipado CatalogId a Guid para la persistencia.</summary>
internal sealed class CatalogIdConverter()
    : ValueConverter<CatalogId, Guid>(id => id.Value, value => new CatalogId(value));
