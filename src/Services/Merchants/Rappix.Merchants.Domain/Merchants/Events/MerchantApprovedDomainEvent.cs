using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Merchants.Domain.Merchants.Events;

/// <summary>Se eleva cuando un admin aprueba un merchant (Pending -> Active).</summary>
public sealed record MerchantApprovedDomainEvent(
    MerchantId MerchantId,
    Guid OwnerUserId,
    string Name,
    string Slug,
    VerticalType VerticalType) : DomainEvent;
