using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Time;
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

        await fixture.Harness.Bus.Publish(Approved(merchantId));
        (await fixture.Harness.Consumed.Any<MerchantApprovedIntegrationEvent>(consumed =>
            consumed.Context.Message.MerchantId == merchantId)).Should().BeTrue();

        await fixture.Harness.Bus.Publish(new MerchantSuspendedIntegrationEvent
        {
            MerchantId = merchantId,
            OwnerUserId = Guid.CreateVersion7(),
            Reason = "Abuso",
        });
        (await fixture.Harness.Consumed.Any<MerchantSuspendedIntegrationEvent>(consumed =>
            consumed.Context.Message.MerchantId == merchantId)).Should().BeTrue();

        MerchantCatalog? catalog = await fixture.GetCatalogAsync(merchantId);
        catalog!.IsEnabled.Should().BeFalse();
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

    private static async Task WaitForConsumedAsync(IConsumerTestHarness<MerchantApprovedConsumer> consumer, Guid merchantId, int expected)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            int consumed = consumer.Consumed
                .Select<MerchantApprovedIntegrationEvent>(received => received.Context.Message.MerchantId == merchantId)
                .Count();
            if (consumed >= expected)
            {
                return;
            }

            await Task.Delay(100);
        }
    }
}
