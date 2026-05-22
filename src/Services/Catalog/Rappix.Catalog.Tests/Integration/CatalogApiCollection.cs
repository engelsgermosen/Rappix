using System.Diagnostics.CodeAnalysis;

namespace Rappix.Catalog.Tests.Integration;

/// <summary>Comparte un unico CatalogApiFactory (y su contenedor PostgreSQL) entre las clases de integracion.</summary>
[CollectionDefinition(Name)]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Convencion de xUnit para definiciones de coleccion.")]
public sealed class CatalogApiCollection : ICollectionFixture<CatalogApiFactory>
{
    public const string Name = "catalog-integration";
}
