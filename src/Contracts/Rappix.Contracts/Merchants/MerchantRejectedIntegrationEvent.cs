using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Merchants;

/// <summary>Publicado cuando un admin rechaza la aprobacion de un merchant.</summary>
public sealed record MerchantRejectedIntegrationEvent : IntegrationEvent
{
    /// <summary>Identificador del merchant.</summary>
    public required Guid MerchantId { get; init; }

    /// <summary>Usuario propietario del merchant.</summary>
    public required Guid OwnerUserId { get; init; }

    /// <summary>Razon del rechazo.</summary>
    public required string Reason { get; init; }
}
