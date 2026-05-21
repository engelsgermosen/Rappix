using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Merchants;

/// <summary>Publicado cuando un merchant suspendido vuelve a Active (reactivacion).</summary>
public sealed record MerchantActivatedIntegrationEvent : IntegrationEvent
{
    /// <summary>Identificador del merchant.</summary>
    public required Guid MerchantId { get; init; }

    /// <summary>Usuario propietario del merchant.</summary>
    public required Guid OwnerUserId { get; init; }
}
