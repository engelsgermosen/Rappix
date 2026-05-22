using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Orders.Application.Responses;

namespace Rappix.Orders.Application.Orders.Place;

/// <summary>
/// Crea (envia) un pedido a partir de una cotizacion (quoteId) y una direccion de entrega. Toma el snapshot
/// del quote desde Pricing (gRPC), congela lineas y precios en el pedido y arranca la saga. Idempotencia de
/// la peticion HTTP via Idempotency-Key (middleware); la saga es idempotente por OrderId.
/// </summary>
public sealed record PlaceOrderCommand(
    Guid CustomerUserId,
    Guid QuoteId,
    string Street,
    string? Reference,
    double Latitude,
    double Longitude) : IRequest<Result<OrderResponse>>;
