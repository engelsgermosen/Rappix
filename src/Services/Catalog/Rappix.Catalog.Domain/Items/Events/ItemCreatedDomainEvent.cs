using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Catalog.Domain.Items.Events;

/// <summary>Se eleva cuando un merchant crea un item en su catalogo.</summary>
public sealed record ItemCreatedDomainEvent(
    Guid MerchantId,
    ItemId ItemId,
    string Name,
    decimal PriceAmount,
    string Currency) : DomainEvent;
