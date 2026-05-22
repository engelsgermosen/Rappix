using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Payments;

/// <summary>
/// Publicado por Orders para reembolsar un pago ya realizado cuando el pedido no puede completarse despues
/// del cobro (p. ej. no hay courier). Lo respondera Payments (fase futura); hoy lo atiende un responder simulado.
/// </summary>
public sealed record RefundRequestedIntegrationEvent : IntegrationEvent
{
    /// <summary>Pedido a reembolsar.</summary>
    public required Guid OrderId { get; init; }

    /// <summary>Monto a reembolsar.</summary>
    public required decimal Amount { get; init; }

    /// <summary>Moneda ISO 4217.</summary>
    public required string Currency { get; init; }

    /// <summary>Razon del reembolso.</summary>
    public required string Reason { get; init; }
}
