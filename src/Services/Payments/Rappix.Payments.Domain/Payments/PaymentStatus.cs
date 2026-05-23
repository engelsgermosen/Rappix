namespace Rappix.Payments.Domain.Payments;

/// <summary>Estados del aggregate <see cref="Payment"/> en el modelo hold+capture.</summary>
public enum PaymentStatus
{
    /// <summary>Aggregate creado pero aun no autorizado contra el gateway.</summary>
    Pending = 0,

    /// <summary>Hold (autorizacion) realizado en el gateway. El dinero esta reservado pero no cobrado al cliente.</summary>
    Authorized = 1,

    /// <summary>Captura ejecutada. El dinero salio de la tarjeta del cliente (terminal feliz).</summary>
    Captured = 2,

    /// <summary>Hold cancelado antes de capturar. Cero cobro al cliente (terminal).</summary>
    Voided = 3,

    /// <summary>El gateway rechazo la autorizacion (tarjeta declinada, etc.) (terminal).</summary>
    Failed = 4,

    /// <summary>Dinero capturado pero la orden fallo despues. Requiere intervencion humana (NO auto-refund).</summary>
    NeedsReview = 5,

    /// <summary>Captura + refund completo ejecutado por solicitud explicita de la saga (terminal).</summary>
    Refunded = 6,
}
