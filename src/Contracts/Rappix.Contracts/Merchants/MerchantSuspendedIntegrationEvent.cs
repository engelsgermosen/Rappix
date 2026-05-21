using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Merchants;

/// <summary>Publicado cuando un admin suspende un merchant.</summary>
public sealed record MerchantSuspendedIntegrationEvent : IntegrationEvent
{
    /// <summary>Identificador del merchant.</summary>
    public required Guid MerchantId { get; init; }

    /// <summary>Usuario propietario del merchant.</summary>
    public required Guid OwnerUserId { get; init; }

    /// <summary>Razon de la suspension.</summary>
    public required string Reason { get; init; }
}
