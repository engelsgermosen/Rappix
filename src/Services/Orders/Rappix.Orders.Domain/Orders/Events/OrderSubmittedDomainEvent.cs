using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Orders.Domain.Orders.Events;

/// <summary>
/// Se eleva cuando el cliente crea (envia) un pedido. La capa de aplicacion lo traduce a
/// OrderSubmittedIntegrationEvent, que se publica por el outbox y ARRANCA la saga (correlacionada por OrderId).
/// </summary>
public sealed record OrderSubmittedDomainEvent(
    OrderId OrderId,
    Guid CustomerUserId,
    Guid MerchantId,
    Guid QuoteId,
    decimal TotalAmount,
    string Currency,
    string DeliveryAddress,
    double DeliveryLatitude,
    double DeliveryLongitude) : DomainEvent;
