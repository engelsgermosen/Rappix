using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Payments;

/// <summary>Publicado cuando un reembolso se completo. Permite a Orders rastrear el reembolso sin bloquear la terminacion de la saga.</summary>
public sealed record RefundCompletedIntegrationEvent : IntegrationEvent
{
    /// <summary>Pedido reembolsado.</summary>
    public required Guid OrderId { get; init; }

    /// <summary>Identificador del reembolso.</summary>
    public required Guid RefundId { get; init; }
}
