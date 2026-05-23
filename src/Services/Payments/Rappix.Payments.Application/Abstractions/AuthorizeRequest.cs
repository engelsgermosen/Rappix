namespace Rappix.Payments.Application.Abstractions;

/// <summary>
/// Solicitud de autorizacion (hold) al gateway. La <see cref="IdempotencyKey"/> es estable por OrderId
/// (<c>$"pmt-auth-{orderId}"</c>); reintentos con la misma key garantizan no doble cobro.
/// </summary>
/// <param name="OrderId">Pedido al que pertenece el pago (correlation id de la saga).</param>
/// <param name="CustomerUserId">Cliente al que se le cobra (metadata para auditoria en el dashboard del proveedor).</param>
/// <param name="Amount">Monto a autorizar.</param>
/// <param name="Currency">Moneda ISO 4217.</param>
/// <param name="IdempotencyKey">Clave de idempotencia estable para reintentos seguros.</param>
/// <param name="CustomerReference">Referencia opcional adicional para auditoria (puede ser null).</param>
public sealed record AuthorizeRequest(
    Guid OrderId,
    Guid CustomerUserId,
    decimal Amount,
    string Currency,
    string IdempotencyKey,
    string? CustomerReference);
