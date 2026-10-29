using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.BuildingBlocks.Testing;
using Rappix.Catalog.Application;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Domain.Catalogs;
using Rappix.Catalog.Infrastructure.Messaging;
using Rappix.Catalog.Infrastructure.Persistence;
using Rappix.Catalog.Infrastructure.Persistence.Repositories;
using Rappix.Contracts.Merchants;
using Testcontainers.PostgreSql;

namespace Rappix.Catalog.Tests.Integration;

/// <summary>
/// Arranca los consumers de eventos de Merchants en un harness in-memory de MassTransit contra un
/// PostgreSQL real, con MediatR, el repositorio y el DbContext reales (sin RabbitMQ ni WebApplicationFactory).
/// </summary>
public sealed class CatalogConsumerHarnessFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:17-3.5")
        .WithDatabase("rappix_catalog")
        .WithUsername("rappix")
        .WithPassword("rappix_test")
        .Build();

    public ServiceProvider Provider { get; private set; } = null!;

    public ITestHarness Harness => Provider.GetRequiredService<ITestHarness>();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCatalogApplication();
        services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<CatalogDbContext>());
        services.AddScoped<ICatalogRepository, CatalogRepository>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.AddConsumer<MerchantApprovedConsumer>();
            configurator.AddConsumer<MerchantActivatedConsumer>();
            configurator.AddConsumer<MerchantSuspendedConsumer>();
        });

        Provider = services.BuildServiceProvider(validateScopes: true);

        using (IServiceScope scope = Provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
        }

        await Harness.Start();
    }

    public async Task<MerchantCatalog?> GetCatalogAsync(Guid merchantId)
    {
        using IServiceScope scope = Provider.CreateScope();
        CatalogDbContext context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        return await context.Catalogs.FirstOrDefaultAsync(catalog => catalog.MerchantId == merchantId);
    }

    public async Task<int> CountCatalogsAsync(Guid merchantId)
    {
        using IServiceScope scope = Provider.CreateScope();
        CatalogDbContext context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        return await context.Catalogs.CountAsync(catalog => catalog.MerchantId == merchantId);
    }

    public async Task DisposeAsync()
    {
        await Harness.Stop();
        await Provider.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

/// <summary>Pruebas de los consumers de eventos de Merchants (choreography Merchants -> Catalog).</summary>
public sealed class MerchantEventConsumerTests(CatalogConsumerHarnessFixture fixture) : IClassFixture<CatalogConsumerHarnessFixture>
{
    [Fact]
    public async Task MerchantApproved_CreatesEnabledCatalog()
    {
        Guid merchantId = Guid.CreateVersion7();

        await fixture.Harness.Bus.Publish(Approved(merchantId));
        (await fixture.Harness.Consumed.Any<MerchantApprovedIntegrationEvent>(consumed =>
            consumed.Context.Message.MerchantId == merchantId)).Should().BeTrue();

        MerchantCatalog? catalog = await fixture.GetCatalogAsync(merchantId);
        catalog.Should().NotBeNull();
        catalog!.IsEnabled.Should().BeTrue();
        catalog.VerticalType.Should().Be(VerticalType.Food);
    }

    [Fact]
    public async Task MerchantSuspended_DisablesCatalog()
    {
        Guid merchantId = Guid.CreateVersion7();

        // Se espera el estado observable en la base (no Consumed.Any): Consumed tambien registra los
        // consumes con fault y su espera depende de la inactividad del harness, que bajo carga (suites en
        // paralelo) puede cortar antes de que el consumer termine.
        await fixture.Harness.Bus.Publish(Approved(merchantId));
        await WaitForCatalogAsync(merchantId, catalog => catalog is { IsEnabled: true });

        await fixture.Harness.Bus.Publish(new MerchantSuspendedIntegrationEvent
        {
            MerchantId = merchantId,
            OwnerUserId = Guid.CreateVersion7(),
            Reason = "Abuso",
        });
        MerchantCatalog catalog = await WaitForCatalogAsync(merchantId, catalog => catalog is { IsEnabled: false });

        catalog.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task MerchantApproved_Twice_CreatesSingleCatalog()
    {
        Guid merchantId = Guid.CreateVersion7();
        MerchantApprovedIntegrationEvent message = Approved(merchantId);

        await fixture.Harness.Bus.Publish(message);
        await fixture.Harness.Bus.Publish(message);

        IConsumerTestHarness<MerchantApprovedConsumer> consumer =
            fixture.Provider.GetRequiredService<IConsumerTestHarness<MerchantApprovedConsumer>>();
        await WaitForConsumedAsync(consumer, merchantId, expected: 2);

        (await fixture.CountCatalogsAsync(merchantId)).Should().Be(1);
    }

    private static MerchantApprovedIntegrationEvent Approved(Guid merchantId) => new()
    {
        MerchantId = merchantId,
        OwnerUserId = Guid.CreateVersion7(),
        Name = "Comercio",
        Slug = $"comercio-{merchantId:N}",
        VerticalType = "Food",
    };

    /// <summary>
    /// Sondea el catalogo del merchant hasta que cumpla <paramref name="condition"/> o venza el timeout
    /// (30 s, holgado para Postgres emulado bajo carga). Al vencer falla con el ultimo estado leido y las
    /// excepciones de los consumes con fault, para que el motivo quede en el reporte del test.
    /// </summary>
    private async Task<MerchantCatalog> WaitForCatalogAsync(Guid merchantId, Func<MerchantCatalog?, bool> condition)
    {
        TimeSpan timeout = TimeSpan.FromSeconds(30);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        MerchantCatalog? catalog;
        do
        {
            catalog = await fixture.GetCatalogAsync(merchantId);
            if (condition(catalog))
            {
                return catalog!;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
        while (stopwatch.Elapsed < timeout);

        string faults = string.Join("; ", fixture.Harness.Consumed.Select(consumed => consumed.Exception is not null)
            .Select(consumed => consumed.Exception!.Message));
        throw new TimeoutException(
            $"El catalogo del merchant {merchantId} no alcanzo el estado esperado en {timeout.TotalSeconds:0} s " +
            $"(ultimo IsEnabled={catalog?.IsEnabled.ToString() ?? "sin catalogo"}; faults: {(faults.Length == 0 ? "ninguno" : faults)}).");
    }

    private static async Task WaitForConsumedAsync(IConsumerTestHarness<MerchantApprovedConsumer> consumer, Guid merchantId, int expected)
    {
        await Eventually.UntilAsync(
            $"MerchantApprovedConsumer consumio {expected} MerchantApproved con MerchantId={merchantId}",
            () => consumer.Consumed.Select<MerchantApprovedIntegrationEvent>(received => received.Context.Message.MerchantId == merchantId).Count(),
            count => count >= expected,
            consumed: consumer.Consumed,
            describe: count => $"{count} consumidos");
    }
}
