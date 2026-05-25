namespace Rappix.Contracts.Orders;

/// <summary>
/// Item del pedido en el snapshot que viaja entre servicios (Fase 13.6). Solo nombre + cantidad —
/// los precios unitarios y totales viven en el agregado Order; aqui llevamos lo minimo para que el
/// courier vea "que" lleva y "cuantos" de cada cosa. Lo consumen <see cref="OrderSubmittedIntegrationEvent"/>
/// y <see cref="Rappix.Contracts.Dispatch.CourierRequestedIntegrationEvent"/>.
/// </summary>
public sealed record OrderLineSnapshot(string ItemName, int Quantity);
