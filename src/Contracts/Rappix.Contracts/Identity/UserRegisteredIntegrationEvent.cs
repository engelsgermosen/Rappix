using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Identity;

/// <summary>
/// Publicado cuando un usuario se registra en la plataforma, ya sea por
/// registro local (email/telefono + password) o via Google.
/// </summary>
public sealed record UserRegisteredIntegrationEvent : IntegrationEvent
{
    /// <summary>Identificador del usuario recien creado.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Email del usuario.</summary>
    public required string Email { get; init; }

    /// <summary>Nombre del usuario.</summary>
    public required string FirstName { get; init; }

    /// <summary>Apellido del usuario.</summary>
    public required string LastName { get; init; }

    /// <summary>Tipo de usuario: Customer, Merchant, Courier o Admin.</summary>
    public required string UserType { get; init; }

    /// <summary>Indica si el email ya quedo confirmado al registrarse (true en altas via Google).</summary>
    public required bool EmailConfirmed { get; init; }
}
