using Rappix.BuildingBlocks.Messaging.Integration;

namespace Rappix.Contracts.Payments;

/// <summary>
/// Publicado por Orders para solicitar el cobro de un pedido. Lo respondera el servicio Payments (fase
/// futura); por ahora lo atiende un responder simulado que emite Succeeded o Failed.
/// </summary>
public sealed record PaymentRequestedIntegrationEvent : IntegrationEvent
{
    /// <summary>Pedido a cobrar (clave de correlacion).</summary>
    public required Guid OrderId { get; init; }

    /// <summary>Cliente a quien se cobra.</summary>
    public required Guid CustomerUserId { get; init; }

    /// <summary>Monto a cobrar.</summary>
    public required decimal Amount { get; init; }

    /// <summary>Moneda ISO 4217.</summary>
    public required string Currency { get; init; }
}
