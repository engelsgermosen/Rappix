using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Identity;

/// <summary>
/// Publicado cuando un usuario confirma su direccion de email.
/// </summary>
public sealed record UserEmailConfirmedIntegrationEvent : IntegrationEvent
{
    /// <summary>Identificador del usuario.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Email confirmado.</summary>
    public required string Email { get; init; }
}
