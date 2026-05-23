// Stub minimo de Fase 8 commit 1. Sera reemplazado por la implementacion completa
// (dual Kestrel, JWT, OpenAPI/Scalar, AddPaymentsInfrastructure, StripeWebhookEndpoint,
// migrate-on-startup guard) en el commit 8.

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { service = "payments", status = "scaffold" }));
app.Run();

/// <summary>Marca de partial class para que WebApplicationFactory&lt;Program&gt; pueda referenciar el host en tests de integracion.</summary>
public partial class Program;
