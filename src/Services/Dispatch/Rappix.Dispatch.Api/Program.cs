using Microsoft.AspNetCore.Server.Kestrel.Core;

// Scaffold inicial (commit 6). Los commits 7-12 incorporan Infrastructure (DbContext + migrations),
// la choreography de UserRegistered, los endpoints REST de courier self-service, el consumer
// CourierRequested + matching geo, y la liberacion en eventos terminales.

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Dos endpoints Kestrel cleartext: 8080 = REST (HTTP/1.1), 8081 = HTTP/2 cleartext (gRPC, reservado
// para Fase 7 Tracking — no se expone servicio aun). HTTP/2 sin TLS no negocia con HTTP/1.1 en el
// mismo puerto, por eso van separados (ADR-0003). No usar ASPNETCORE_URLS.
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenAnyIP(8080, listen => listen.Protocols = HttpProtocols.Http1);
    kestrel.ListenAnyIP(8081, listen => listen.Protocols = HttpProtocols.Http2);
});

WebApplication app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { service = "dispatch", status = "scaffold" }));

app.Run();

/// <summary>Punto de entrada expuesto como partial para WebApplicationFactory en pruebas.</summary>
public partial class Program;
