using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Merchants;

/// <summary>Publicado cuando un admin aprueba un merchant (pasa a Active por primera vez).</summary>
public sealed record MerchantApprovedIntegrationEvent : IntegrationEvent
{
    /// <summary>Identificador del merchant.</summary>
    public required Guid MerchantId { get; init; }

    /// <summary>Usuario propietario del merchant.</summary>
    public required Guid OwnerUserId { get; init; }

    /// <summary>Nombre comercial.</summary>
    public required string Name { get; init; }

    /// <summary>Slug unico.</summary>
    public required string Slug { get; init; }

    /// <summary>Vertical: Food, Pharmacy, Grocery o Parcel.</summary>
    public required string VerticalType { get; init; }
}
