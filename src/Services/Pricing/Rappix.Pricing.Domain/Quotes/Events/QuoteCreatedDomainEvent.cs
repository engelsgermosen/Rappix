using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Pricing.Domain.Quotes.Events;

/// <summary>Se eleva cuando se crea una cotizacion. La capa de aplicacion lo traduce a evento de integracion.</summary>
public sealed record QuoteCreatedDomainEvent(
    QuoteId QuoteId,
    Guid CustomerUserId,
    Guid MerchantId,
    decimal Total,
    string Currency,
    DateTime ExpiresAtUtc) : DomainEvent;
