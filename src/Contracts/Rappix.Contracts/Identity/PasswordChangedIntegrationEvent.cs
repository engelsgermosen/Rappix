using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Identity;

/// <summary>
/// Publicado cuando un usuario cambia su contrasena. Util para que servicios
/// como Notifications envien una alerta de seguridad.
/// </summary>
public sealed record PasswordChangedIntegrationEvent : IntegrationEvent
{
    /// <summary>Identificador del usuario.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Email del usuario.</summary>
    public required string Email { get; init; }
}
