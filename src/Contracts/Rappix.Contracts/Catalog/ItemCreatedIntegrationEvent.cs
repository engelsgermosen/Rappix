using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Catalog;

/// <summary>Publicado cuando un merchant crea un item en su catalogo.</summary>
public sealed record ItemCreatedIntegrationEvent : IntegrationEvent
{
    /// <summary>Merchant dueno del catalogo.</summary>
    public required Guid MerchantId { get; init; }

    /// <summary>Identificador del item.</summary>
    public required Guid ItemId { get; init; }

    /// <summary>Nombre del item.</summary>
    public required string Name { get; init; }

    /// <summary>Precio base del item (la logica de pricing dinamico vive en el servicio Pricing).</summary>
    public required decimal BasePriceAmount { get; init; }

    /// <summary>Moneda ISO 4217 del precio base (p. ej. DOP).</summary>
    public required string Currency { get; init; }
}
