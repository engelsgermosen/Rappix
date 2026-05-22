using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Rappix.BuildingBlocks.Core.Storage;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Rappix.Catalog.Tests.Integration;

/// <summary>
/// Factory de pruebas que arranca la API contra un PostgreSQL efimero (Testcontainers), stubea el
/// almacenamiento de objetos (MinIO) y el cliente gRPC de Merchants (NSubstitute, sin levantar
/// Merchants), y neutraliza los hosted services (sin RabbitMQ). El outbox sigue escribiendo en la BD.
/// </summary>
public sealed class CatalogApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:17-3.5")
        .WithDatabase("rappix_catalog")
        .WithUsername("rappix")
        .WithPassword("rappix_test")
        .Build();

    /// <summary>Stub del almacenamiento de fotos (no requiere MinIO).</summary>
    public IObjectStorage ObjectStorage { get; } = Substitute.For<IObjectStorage>();

    /// <summary>Stub del cliente gRPC de Merchants. Por defecto: servicio disponible y merchant activo.</summary>
    public IMerchantValidationClient MerchantValidationClient { get; } = Substitute.For<IMerchantValidationClient>();

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

            // Cliente gRPC de Merchants stubeado: por defecto el merchant esta activo.
            services.RemoveAll<IMerchantValidationClient>();
            services.AddSingleton(MerchantValidationClient);
            MerchantValidationClient
                .ValidateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(new MerchantValidation(ServiceAvailable: true, Exists: true, IsActive: true));
        });
    }

    /// <summary>Ejecuta una accion de siembra en un scope con el DbContext y persiste los cambios.</summary>
    public async Task SeedAsync(Func<CatalogDbContext, Task> seed)
    {
        using IServiceScope scope = Services.CreateScope();
        CatalogDbContext context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await seed(context);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // WebApplication.CreateBuilder lee variables de entorno, asi que estan disponibles al arrancar.
        Environment.SetEnvironmentVariable("ConnectionStrings__CatalogDb", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__SigningKey", TestTokens.SigningKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestTokens.Issuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestTokens.Audience);

        using IServiceScope scope = Services.CreateScope();
        CatalogDbContext context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await context.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
