using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Orders.Domain.Orders.Events;

/// <summary>
/// Se eleva cuando el cliente crea (envia) un pedido. La capa de aplicacion lo traduce a
/// OrderSubmittedIntegrationEvent, que se publica por el outbox y ARRANCA la saga (correlacionada por OrderId).
/// Fase 13.6: incluye MerchantName, DeliveryReference y Lines (lista de (ItemName, Quantity)) para
/// que la saga propague el snapshot a CourierRequested.
/// </summary>
public sealed record OrderSubmittedDomainEvent(
    OrderId OrderId,
    Guid CustomerUserId,
    Guid MerchantId,
    string MerchantName,
    Guid QuoteId,
    decimal TotalAmount,
    string Currency,
    string DeliveryAddress,
    string? DeliveryReference,
    double DeliveryLatitude,
    double DeliveryLongitude,
    double PickupLatitude,
    double PickupLongitude,
    IReadOnlyList<OrderLineDomainSnapshot> Lines) : DomainEvent;

/// <summary>
/// Snapshot de una linea del pedido a nivel de dominio (no es un Event by si mismo). Se proyecta a
/// OrderLineSnapshot de Rappix.Contracts.Orders en el handler. Vive aqui (Domain) para que el aggregate
/// pueda construirlo sin depender del proyecto de contratos.
/// </summary>
public sealed record OrderLineDomainSnapshot(string ItemName, int Quantity);
