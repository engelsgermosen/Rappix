using System.Globalization;
using System.Text;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Rappix.Tracking.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Rappix.Tracking.Tests.Integration.Hubs;

/// <summary>
/// Arranca la API REAL de Tracking (Program.cs + AddTrackingInfrastructure + AddRappixMessaging) contra
/// PostgreSQL y RabbitMQ efimeros (Testcontainers). Sin gRPC para mockear: Tracking no es cliente de
/// ningun servicio. Expone helpers para generar JWT validos y consultar el DbContext desde los tests.
/// </summary>
/// <remarks>
/// El test usa el TRANSPORT LongPolling porque <c>TestServer</c> no soporta WebSockets nativos —
/// HttpMessageHandlerFactory => factory.Server.CreateHandler() encapsula el pipeline en memoria.
/// </remarks>
public sealed class TrackingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtIssuer = "https://localhost:5001";
    public const string JwtAudience = "rappix";
    public const string JwtSigningKey = "rappix-test-signing-key-please-change-32bytes-minimum";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("rappix_tracking")
        .WithUsername("rappix")
        .WithPassword("rappix_test")
        .Build();

    private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-management-alpine")
        .WithUsername("rappix")
        .WithPassword("rappix_test")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Testing");

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _rabbitmq.StartAsync();

        // Program.cs lee las env vars al CreateBuilder; se fijan ANTES de tocar Services.
        Environment.SetEnvironmentVariable("ConnectionStrings__TrackingDb", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("RabbitMq__Host", _rabbitmq.Hostname);
        Environment.SetEnvironmentVariable("RabbitMq__Port",
            _rabbitmq.GetMappedPublicPort(5672).ToString(CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable("RabbitMq__Username", "rappix");
        Environment.SetEnvironmentVariable("RabbitMq__Password", "rappix_test");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", JwtSigningKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", JwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", JwtAudience);

        // Migra el schema ANTES de arrancar el host (los hosted services asumen schema presente).
        DbContextOptions<TrackingDbContext> options = new DbContextOptionsBuilder<TrackingDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var migrationContext = new TrackingDbContext(options, Substitute.For<IPublisher>());
        await migrationContext.Database.MigrateAsync();
    }

    /// <summary>Crea un JWT para el userId dado con el mismo signing key/issuer/audience que Identity.</summary>
    public static string CreateJwt(Guid userId)
    {
        var handler = new JsonWebTokenHandler();
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey)),
            SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = JwtIssuer,
            Audience = JwtAudience,
            NotBefore = DateTime.UtcNow.AddSeconds(-30),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = credentials,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = userId.ToString(),
                ["email"] = $"{userId}@test.local",
                ["userType"] = "Customer",
            },
        };
        return handler.CreateToken(descriptor);
    }

    /// <summary>Espera a que el bus interno este sano antes de publicar (WaitUntilStarted=false en MT).</summary>
    public async Task WaitForBusHealthyAsync(TimeSpan timeout)
    {
        IBusControl busControl = Services.GetRequiredService<IBusControl>();
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            if (busControl.CheckHealth().Status == BusHealthStatus.Healthy)
            {
                return;
            }
            await Task.Delay(100, cts.Token);
        }
        throw new TimeoutException($"Bus no sano en {timeout}.");
    }

    /// <summary>Scope con DbContext para hacer seed/asserts directos contra la BD.</summary>
    public (IServiceScope Scope, TrackingDbContext Db) CreateDbScope()
    {
        IServiceScope scope = Services.CreateScope();
        return (scope, scope.ServiceProvider.GetRequiredService<TrackingDbContext>());
    }

    public async new Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _rabbitmq.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
