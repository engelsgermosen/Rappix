using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Orders.Application.Orders.MarkDelivered;

/// <summary>
/// Marca un pedido como entregado (SEAM TEMPORAL hacia Dispatch, Fase 6). Publica OrderDeliveredIntegrationEvent
/// que lleva la saga de InProgress a Completed. Cuando llegue Dispatch real, este comando/endpoint se elimina.
/// </summary>
public sealed record MarkDeliveredCommand(Guid OrderId) : IRequest<Result>;
