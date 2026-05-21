using System.Diagnostics.CodeAnalysis;

namespace Rappix.Merchants.Tests.Integration;

/// <summary>Comparte un unico MerchantsApiFactory (y su contenedor PostGIS) entre las clases de integracion.</summary>
[CollectionDefinition(Name)]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Convencion de xUnit para definiciones de coleccion.")]
public sealed class MerchantsApiCollection : ICollectionFixture<MerchantsApiFactory>
{
    public const string Name = "merchants-integration";
}
