using System.Diagnostics.CodeAnalysis;

namespace Rappix.Pricing.Tests.Integration;

/// <summary>Comparte un unico PricingApiFactory (y su contenedor PostgreSQL) entre las clases de integracion.</summary>
[CollectionDefinition(Name)]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Convencion de xUnit para definiciones de coleccion.")]
public sealed class PricingApiCollection : ICollectionFixture<PricingApiFactory>
{
    public const string Name = "pricing-integration";
}
