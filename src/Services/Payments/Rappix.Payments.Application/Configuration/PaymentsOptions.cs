namespace Rappix.Payments.Application.Configuration;

/// <summary>Opciones del servicio Payments (seccion <c>Payments</c> en appsettings/env).</summary>
public sealed class PaymentsOptions
{
    /// <summary>Nombre de la seccion en appsettings.</summary>
    public const string SectionName = "Payments";

    /// <summary>
    /// Selector de gateway: <c>"Fake"</c> (default; in-process, sin red, smoke E2E sin Stripe) o
    /// <c>"Stripe"</c> (PaymentIntent real con <c>CaptureMethod=manual</c>, requiere Stripe.ApiKey).
    /// Documentado en ADR-0009 #1.
    /// </summary>
    public string Gateway { get; set; } = "Fake";

    /// <summary>Configuracion del gateway Stripe (solo aplicable si <see cref="Gateway"/> = "Stripe").</summary>
    public StripeOptions Stripe { get; set; } = new();

    /// <summary>Configuracion del gateway Fake (in-process).</summary>
    public FakeOptions Fake { get; set; } = new();

    /// <summary>Configuracion del gateway Stripe.</summary>
    public sealed class StripeOptions
    {
        /// <summary>Secret API Key de Stripe (sk_test_... o sk_live_...). Lee de env <c>STRIPE_API_KEY</c>.</summary>
        public string? ApiKey { get; set; }

        /// <summary>Webhook signing secret (whsec_...) para validar la firma en el endpoint
        /// <c>/api/v1/payments/webhooks/stripe</c>. Lee de env <c>STRIPE_WEBHOOK_SECRET</c>.</summary>
        public string? WebhookSecret { get; set; }
    }

    /// <summary>Configuracion del gateway Fake.</summary>
    public sealed class FakeOptions
    {
        /// <summary>
        /// Modo de operacion del Fake:
        /// <list type="bullet">
        /// <item><c>"Always"</c> (default): siempre devuelve OK. Usado por el smoke E2E y los tests de happy path.</item>
        /// <item><c>"AlwaysFail"</c>: AuthorizeAsync devuelve <c>Result.Failure</c>. Para tests del camino fallido.</item>
        /// </list>
        /// </summary>
        public string Mode { get; set; } = "Always";
    }
}
