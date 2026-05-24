namespace Rappix.Gateway.Api.OpenApi;

/// <summary>
/// Endpoint raiz del gateway: HTML estatico con links a los 9 Scalar UIs (en sus puertos host
/// del docker-compose) y al endpoint /health agregado. Decision Fase 10: NO consolidamos los
/// 9 OpenAPI documents en un solo Scalar (sobre-ingenieria); el follow-up esta en ADR-0011.
/// </summary>
public static class ApiDocsLandingEndpoint
{
    /// <summary>Registra <c>GET /</c> devolviendo el HTML estatico con los links.</summary>
    public static IEndpointRouteBuilder MapApiDocsLanding(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/", () => Results.Content(
            """
            <!DOCTYPE html>
            <html lang="es">
              <head>
                <meta charset="utf-8">
                <title>Rappix API Gateway</title>
                <style>
                  body { font-family: -apple-system, Segoe UI, Roboto, Arial, sans-serif; padding: 32px; max-width: 720px; margin: auto; color: #1f2937; }
                  h1 { font-size: 24px; margin-bottom: 8px; }
                  p { color: #4b5563; }
                  ul { line-height: 1.8; }
                  a { color: #2563eb; text-decoration: none; }
                  a:hover { text-decoration: underline; }
                  code { background: #f3f4f6; padding: 2px 6px; border-radius: 4px; }
                </style>
              </head>
              <body>
                <h1>Rappix API Gateway (Fase 10)</h1>
                <p>Punto de entrada unico en <code>http://localhost:5000</code>. La UI Scalar de cada servicio sigue accesible en su puerto directo:</p>
                <ul>
                  <li><a href="http://localhost:5001/scalar/v1">Identity</a> &mdash; auth + users</li>
                  <li><a href="http://localhost:5002/scalar/v1">Merchants</a> &mdash; PostGIS + admin</li>
                  <li><a href="http://localhost:5003/scalar/v1">Catalog</a> &mdash; menu + reservation</li>
                  <li><a href="http://localhost:5004/scalar/v1">Pricing</a> &mdash; quote + coupons</li>
                  <li><a href="http://localhost:5005/scalar/v1">Orders</a> &mdash; saga + state machine</li>
                  <li><a href="http://localhost:5006/scalar/v1">Dispatch</a> &mdash; courier matching</li>
                  <li><a href="http://localhost:5007/scalar/v1">Tracking</a> &mdash; SignalR hub</li>
                  <li><a href="http://localhost:5008/scalar/v1">Payments</a> &mdash; Stripe + webhook</li>
                  <li><a href="http://localhost:5009/scalar/v1">Notifications</a> &mdash; email channel</li>
                </ul>
                <p><a href="/health">/health</a> &mdash; estado agregado de los 9 servicios.</p>
                <p><a href="/health/live">/health/live</a> &mdash; liveness del propio gateway.</p>
              </body>
            </html>
            """, "text/html"))
            .AllowAnonymous();   // sin esto, FallbackPolicy=RequireAuthenticatedUser bloquearia el landing.

        return endpoints;
    }
}
