using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Rappix.Merchants.Tests.Integration;

/// <summary>
/// Factory de pruebas que arranca la API contra un PostgreSQL+PostGIS efimero (Testcontainers),
/// stubea el almacenamiento de objetos (MinIO) y neutraliza los hosted services (sin RabbitMQ).
/// El outbox sigue escribiendo en la BD de forma transaccional.
/// </summary>
public sealed class MerchantsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:17-3.5")
        .WithDatabase("rappix_merchants")
        .WithUsername("rappix")
        .WithPassword("rappix_test")
        .Build();

    /// <summary>Stub del almacenamiento de logos (no requiere MinIO).</summary>
    public IObjectStorage ObjectStorage { get; } = Substitute.For<IObjectStorage>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Sin hosted services: el bus de MassTransit no conecta a RabbitMQ y el inicializador de MinIO no corre.
            services.RemoveAll<IHostedService>();

            services.RemoveAll<IObjectStorage>();
            services.AddSingleton(ObjectStorage);

            ObjectStorage
                .UploadAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult((string)call[0]!));
            ObjectStorage
                .GetPresignedUrlAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult($"https://minio.test/{(string)call[0]!}"));
        });
    }

    /// <summary>Ejecuta una accion de siembra en un scope con el DbContext y persiste los cambios.</summary>
    public async Task SeedAsync(Func<MerchantsDbContext, Task> seed)
    {
        using IServiceScope scope = Services.CreateScope();
        MerchantsDbContext context = scope.ServiceProvider.GetRequiredService<MerchantsDbContext>();
        await seed(context);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // WebApplication.CreateBuilder lee variables de entorno, asi que estan disponibles al arrancar.
        Environment.SetEnvironmentVariable("ConnectionStrings__MerchantsDb", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__SigningKey", TestTokens.SigningKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestTokens.Issuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestTokens.Audience);

        using IServiceScope scope = Services.CreateScope();
        MerchantsDbContext context = scope.ServiceProvider.GetRequiredService<MerchantsDbContext>();
        await context.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
