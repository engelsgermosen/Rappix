using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Identity.Domain.Users.Events;

/// <summary>Se eleva cuando un usuario inicia sesion satisfactoriamente.</summary>
public sealed record UserLoggedInDomainEvent(UserId UserId) : DomainEvent;
