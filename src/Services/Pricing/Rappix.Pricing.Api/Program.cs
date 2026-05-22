WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();
app.MapGet("/", () => "Rappix Pricing API");
app.Run();

/// <summary>Punto de entrada expuesto como partial para WebApplicationFactory en pruebas.</summary>
public partial class Program;
