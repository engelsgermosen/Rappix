using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Catalog;

/// <summary>Publicado cuando el stock de un item llega a cero (permite ocultarlo o avisar al merchant).</summary>
public sealed record StockDepletedIntegrationEvent : IntegrationEvent
{
    /// <summary>Merchant dueno del catalogo.</summary>
    public required Guid MerchantId { get; init; }

    /// <summary>Identificador del item agotado.</summary>
    public required Guid ItemId { get; init; }

    /// <summary>Nombre del item agotado.</summary>
    public required string Name { get; init; }
}
