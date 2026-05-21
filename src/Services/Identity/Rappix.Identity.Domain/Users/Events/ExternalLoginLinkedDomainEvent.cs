using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Identity.Domain.Users.Events;

/// <summary>Se eleva cuando se vincula un login externo (p.ej. Google) a un usuario.</summary>
public sealed record ExternalLoginLinkedDomainEvent(
    UserId UserId,
    string Provider,
    string ProviderKey,
    string Email) : DomainEvent;
