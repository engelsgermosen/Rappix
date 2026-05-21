using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Identity.Application.Authentication;
using Rappix.Identity.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Rappix.Identity.Tests.Integration;

/// <summary>
/// Factory de pruebas que arranca la API contra un PostgreSQL efimero (Testcontainers),
/// mockea el envio de email y la autenticacion externa, y neutraliza los hosted services
/// (para no requerir RabbitMQ). El outbox sigue escribiendo en la BD de forma transaccional.
/// </summary>
public sealed class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("rappix_identity")
        .WithUsername("rappix")
        .WithPassword("rappix_test")
        .Build();

    /// <summary>Mock del envio de email; permite capturar el enlace de confirmacion.</summary>
    public IEmailSender EmailSender { get; } = Substitute.For<IEmailSender>();

    /// <summary>Stub de la autenticacion externa (Google) para pruebas del callback.</summary>
    public IExternalAuthService ExternalAuth { get; } = Substitute.For<IExternalAuthService>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Sin hosted services: el bus de MassTransit no intenta conectar a RabbitMQ.
            services.RemoveAll<IHostedService>();

            services.RemoveAll<IEmailSender>();
            services.AddScoped(_ => EmailSender);

            services.RemoveAll<IExternalAuthService>();
            services.AddScoped(_ => ExternalAuth);

            EmailSender
                .SendEmailConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(Result.Success()));
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Las variables de entorno las lee WebApplication.CreateBuilder (AddEnvironmentVariables),
        // por lo que estan disponibles cuando Program.cs lee la configuracion al arrancar.
        Environment.SetEnvironmentVariable("ConnectionStrings__IdentityDb", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "test-signing-key-rappix-identity-0123456789-abcdefghij");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "https://localhost:5001");
        Environment.SetEnvironmentVariable("Jwt__Audience", "rappix");
        Environment.SetEnvironmentVariable("App__PublicBaseUrl", "https://localhost:5001");

        using IServiceScope scope = Services.CreateScope();
        IdentityDbContext context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await context.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
