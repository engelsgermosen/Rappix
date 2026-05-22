// Scaffold inicial del host. El wiring completo (Kestrel dual-port REST/gRPC, JWT, OpenAPI,
// capas Application/Infrastructure, endpoints y CatalogValidationService) se agrega en el Paso 7.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

WebApplication app = builder.Build();

app.Run();
