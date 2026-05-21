using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Identity.Domain.Users.Events;

/// <summary>Se eleva cuando un usuario cambia su contrasena.</summary>
public sealed record PasswordChangedDomainEvent(UserId UserId, string Email) : DomainEvent;
