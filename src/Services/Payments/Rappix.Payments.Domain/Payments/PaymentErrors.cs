using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Payments.Domain.Payments;

/// <summary>Errores del value object <see cref="Money"/> para Payments.</summary>
public static class MoneyErrors
{
    /// <summary>Monto cero o negativo (no se cobran montos no positivos).</summary>
    public static readonly Error NonPositive =
        Error.Validation("Payments.Money.NonPositive", "El monto debe ser mayor a cero.");

    /// <summary>Moneda invalida (debe ser ISO 4217: 3 letras ASCII).</summary>
    public static readonly Error InvalidCurrency =
        Error.Validation("Payments.Money.InvalidCurrency", "La moneda debe ser un codigo ISO 4217 de 3 letras.");
}

/// <summary>Errores del aggregate <see cref="Payment"/>.</summary>
public static class PaymentErrors
{
    /// <summary>El pago ya esta autorizado con un PaymentIntent distinto (race entre dos consumers o gateway flake).</summary>
    public static readonly Error AlreadyAuthorizedWithDifferentIntent =
        Error.Conflict(
            "Payments.Payment.AlreadyAuthorizedWithDifferentIntent",
            "El pago ya esta autorizado con un PaymentIntent distinto.");

    /// <summary>Se intenta capturar/void desde un estado donde no hay hold (Pending).</summary>
    public static readonly Error NotAuthorized =
        Error.Conflict(
            "Payments.Payment.NotAuthorized",
            "No se puede operar (capture/void) sobre un pago que no esta autorizado.");

    /// <summary>Transicion bloqueada porque el pago ya esta en un estado terminal incompatible.</summary>
    public static readonly Error TerminalState =
        Error.Conflict(
            "Payments.Payment.TerminalState",
            "El pago esta en un estado terminal y no acepta la transicion solicitada.");

    /// <summary>MarkNeedsReview o MarkRefunded sin captura previa.</summary>
    public static readonly Error NotCaptured =
        Error.Conflict(
            "Payments.Payment.NotCaptured",
            "Solo se puede refundar o marcar para revision un pago capturado.");

    /// <summary>Refund ya registrado con un RefundId distinto (no se permite doble-refund).</summary>
    public static readonly Error AlreadyRefundedWithDifferentId =
        Error.Conflict(
            "Payments.Payment.AlreadyRefundedWithDifferentId",
            "El pago ya tiene un refund registrado con un identificador distinto.");
}
