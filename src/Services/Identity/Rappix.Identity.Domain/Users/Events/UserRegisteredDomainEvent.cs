using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Identity.Domain.Users.Events;

/// <summary>Se eleva cuando un usuario se registra (local o via Google).</summary>
public sealed record UserRegisteredDomainEvent(
    UserId UserId,
    string Email,
    string FirstName,
    string LastName,
    UserType UserType,
    bool EmailConfirmed) : DomainEvent;
