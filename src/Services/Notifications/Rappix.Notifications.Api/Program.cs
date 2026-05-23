// Stub minimo de Fase 9 commit 1. La wiring completa (JWT, OpenAPI, DI de capas, migrate-on-startup,
// endpoints y ConfigureKestrel) llega en commit 11. Por ahora solo se necesita un Program.cs que
// compile para que la solucion arme limpia y los csproj queden conectados en la sln.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();
app.Run();
