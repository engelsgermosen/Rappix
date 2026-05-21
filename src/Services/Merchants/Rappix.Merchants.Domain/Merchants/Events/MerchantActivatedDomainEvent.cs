using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Merchants.Domain.Merchants.Events;

/// <summary>Se eleva cuando un merchant suspendido vuelve a Active (reactivacion).</summary>
public sealed record MerchantActivatedDomainEvent(MerchantId MerchantId, Guid OwnerUserId) : DomainEvent;
