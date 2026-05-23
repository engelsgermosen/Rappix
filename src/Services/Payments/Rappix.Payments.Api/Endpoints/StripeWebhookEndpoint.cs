using Microsoft.Extensions.Options;
using Rappix.Payments.Application.Configuration;
using Stripe;

namespace Rappix.Payments.Api.Endpoints;

/// <summary>
/// Endpoint webhook de Stripe: <c>POST /api/v1/payments/webhooks/stripe</c>. La autenticacion es por
/// FIRMA del header <c>Stripe-Signature</c> (no JWT — Stripe firma con el WebhookSecret compartido).
/// </summary>
/// <remarks>
/// <b>En Fase 8 el handler es INFORMATIVO</b>: log estructurado a Seq para los eventos relevantes,
/// sin mutar estado. Justificacion: el flujo principal con <c>pm_card_visa</c> +
/// <c>CaptureMethod=manual</c> + <c>Confirm=true</c> en Stripe test mode es SINCRONO — <c>AuthorizeAsync</c>
/// devuelve <c>requires_capture</c> directo y <c>CaptureAsync</c> devuelve <c>succeeded</c> directo.
/// El webhook seria obligatorio solo para flujos async (3DS challenge, async confirmation con payment
/// methods reales del cliente) que estan fuera de scope Fase 8 (sin frontend con Stripe Elements,
/// ADR-0009 #8). Follow-up: implementar handler full async cuando llegue 3DS.
/// <para>
/// Con <c>Payments:Gateway=Fake</c> el endpoint queda accesible pero sin <c>WebhookSecret</c>
/// configurado todas las requests devuelven 400 (fail-safe — no hay forma de verificar firmas sin la
/// clave compartida). Para usar Stripe en local: <c>stripe listen --forward-to localhost:5008/api/v1/payments/webhooks/stripe</c>
/// y exportar el <c>whsec_xxx</c> como <c>STRIPE_WEBHOOK_SECRET</c> en .env.
/// </para>
/// </remarks>
public static partial class StripeWebhookEndpoint
{
    /// <summary>Registra el endpoint webhook en el route builder.</summary>
    public static IEndpointRouteBuilder MapStripeWebhook(this IEndpointRouteBuilder app)
    {
        app.MapPost("/payments/webhooks/stripe", HandleAsync)
           .AllowAnonymous() // autenticacion = firma del header, no JWT.
           .WithName("StripeWebhook")
           .WithSummary("Recibe eventos webhook de Stripe (firma validada via Stripe-Signature).");

        return app;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        IOptions<PaymentsOptions> options,
        ILogger<StripeWebhookEndpointMarker> logger,
        CancellationToken cancellationToken)
    {
        // Solo se procesa si Gateway=Stripe Y WebhookSecret esta configurado. En cualquier otro caso
        // -> 400 (fail-safe: sin clave compartida no hay forma de verificar firmas).
        string? webhookSecret = options.Value.Stripe?.WebhookSecret;
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return Results.BadRequest(new
            {
                error = "webhook_not_configured",
                message = "Stripe WebhookSecret no configurado (Payments:Stripe:WebhookSecret o STRIPE_WEBHOOK_SECRET en env).",
            });
        }

        // Leer el body como texto (Stripe espera el JSON crudo para validar la firma).
        string json;
        using (var reader = new StreamReader(request.Body))
        {
            json = await reader.ReadToEndAsync(cancellationToken);
        }

        string? signatureHeader = request.Headers["Stripe-Signature"];
        if (string.IsNullOrEmpty(signatureHeader))
        {
            return Results.BadRequest(new { error = "missing_signature_header" });
        }

        Event stripeEvent;
        try
        {
            // ConstructEvent valida la firma; lanza StripeException si invalida (timestamp viejo,
            // firma no coincide, secret distinto, etc.). Cualquier 400 aqui es legitimamente
            // "request no autorizada" — un atacante no puede falsificar eventos sin el WebhookSecret.
            stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, webhookSecret);
        }
        catch (StripeException ex)
        {
            string message = ex.Message;
            LogInvalidSignature(logger, message);
            return Results.BadRequest(new { error = "invalid_signature", message });
        }

        // Handler INFORMATIVO en Fase 8: solo loguea. NO muta estado. Locales extraidos antes del
        // Log* para evitar CA1873 (los args no deben ser property-chain en el sitio del log).
        string eventType = stripeEvent.Type;
        string eventId = stripeEvent.Id;
        switch (eventType)
        {
            case "payment_intent.succeeded":
            case "payment_intent.payment_failed":
            case "payment_intent.canceled":
            case "payment_intent.requires_action":
            case "charge.refunded":
            case "charge.dispute.created":
                LogWebhookReceived(logger, eventType, eventId);
                break;

            default:
                LogWebhookIgnored(logger, eventType, eventId);
                break;
        }

        return Results.Ok();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook Stripe con firma invalida: {Message}")]
    private static partial void LogInvalidSignature(ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Information, Message = "Webhook Stripe recibido: type={Type} id={EventId}")]
    private static partial void LogWebhookReceived(ILogger logger, string type, string eventId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Webhook Stripe ignorado (tipo no manejado): type={Type} id={EventId}")]
    private static partial void LogWebhookIgnored(ILogger logger, string type, string eventId);

    /// <summary>
    /// Marker class usada como TCategoryName para el <see cref="ILogger{TCategoryName}"/> del
    /// handler. Mantiene el log estructurado bajo una categoria especifica de Payments.Webhooks.
    /// </summary>
    public sealed class StripeWebhookEndpointMarker;
}
