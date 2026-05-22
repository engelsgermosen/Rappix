using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Rappix.Pricing.Tests.Integration;

/// <summary>
/// Factory de pruebas que arranca la API contra un PostgreSQL efimero (Testcontainers) y stubea los
/// clientes gRPC de Catalog (precios) y Merchants (validacion) con NSubstitute, sin levantar esos
/// servicios. Neutraliza los hosted services (sin RabbitMQ); el outbox sigue escribiendo en la BD.
/// </summary>
public sealed class PricingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:17-3.5")
        .WithDatabase("rappix_pricing")
        .WithUsername("rappix")
        .WithPassword("rappix_test")
        .Build();

    /// <summary>Stub del cliente gRPC de Catalog. Por defecto: item encontrado, comprable, precio 100 DOP.</summary>
    public ICatalogPricingClient CatalogClient { get; } = Substitute.For<ICatalogPricingClient>();

    /// <summary>Stub del cliente gRPC de Merchants. Por defecto: servicio disponible, merchant activo, vertical Food.</summary>
    public IMerchantPricingClient MerchantClient { get; } = Substitute.For<IMerchantPricingClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Sin hosted services: el bus de MassTransit no conecta a RabbitMQ. El outbox sigue escribiendo en la BD.
            services.RemoveAll<IHostedService>();

            services.RemoveAll<ICatalogPricingClient>();
            services.AddSingleton(CatalogClient);
            CatalogClient
                .GetItemPricingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(call => new CatalogItemPricing(
                    ServiceAvailable: true, Found: true, (Guid)call[0]!, Guid.CreateVersion7(), "Item", 100m, "DOP", IsPurchasable: true));

            services.RemoveAll<IMerchantPricingClient>();
            services.AddSingleton(MerchantClient);
            MerchantClient
                .GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(new MerchantPricingInfo(ServiceAvailable: true, Found: true, IsActive: true, VerticalType: "Food"));
        });
    }

    /// <summary>Ejecuta una accion de siembra en un scope con el DbContext y persiste los cambios.</summary>
    public async Task SeedAsync(Func<PricingDbContext, Task> seed)
    {
        using IServiceScope scope = Services.CreateScope();
        PricingDbContext context = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        await seed(context);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__PricingDb", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__SigningKey", TestTokens.SigningKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestTokens.Issuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestTokens.Audience);

        using IServiceScope scope = Services.CreateScope();
        PricingDbContext context = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        await context.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
