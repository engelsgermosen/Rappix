using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Catalog.Domain.Items.Events;

/// <summary>Se eleva cuando el stock de un item llega a cero.</summary>
public sealed record StockDepletedDomainEvent(Guid MerchantId, ItemId ItemId) : DomainEvent;
