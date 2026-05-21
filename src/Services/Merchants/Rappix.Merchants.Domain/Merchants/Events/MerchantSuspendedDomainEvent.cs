using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Merchants.Domain.Merchants.Events;

/// <summary>Se eleva cuando un admin suspende un merchant.</summary>
public sealed record MerchantSuspendedDomainEvent(
    MerchantId MerchantId,
    Guid OwnerUserId,
    string Reason) : DomainEvent;
