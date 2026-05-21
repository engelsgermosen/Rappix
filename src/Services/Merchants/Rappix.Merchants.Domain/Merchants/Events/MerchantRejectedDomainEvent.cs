using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Merchants.Domain.Merchants.Events;

/// <summary>Se eleva cuando un admin rechaza la aprobacion (Pending -> Rejected).</summary>
public sealed record MerchantRejectedDomainEvent(
    MerchantId MerchantId,
    Guid OwnerUserId,
    string Reason) : DomainEvent;
