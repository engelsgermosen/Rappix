using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Identity.Domain.Users.Events;

/// <summary>Se eleva cuando un usuario confirma su email.</summary>
public sealed record EmailConfirmedDomainEvent(UserId UserId, string Email) : DomainEvent;
