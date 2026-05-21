using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Identity;
using Rappix.Merchants.Application;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Infrastructure.Messaging;
using Rappix.Merchants.Infrastructure.Persistence;
using Rappix.Merchants.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace Rappix.Merchants.Tests.Integration;

/// <summary>
/// Arranca el consumer en un harness de MassTransit in-memory contra un PostgreSQL+PostGIS real,
/// con MediatR, el repositorio y el DbContext reales (sin RabbitMQ ni WebApplicationFactory).
/// </summary>
public sealed class ConsumerHarnessFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:17-3.5")
        .WithDatabase("rappix_merchants")
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
        services.AddMerchantsApplication();
        services.AddDbContext<MerchantsDbContext>(options =>
            options.UseNpgsql(_postgres.GetConnectionString(), npgsql => npgsql.UseNetTopologySuite()));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<MerchantsDbContext>());
        services.AddScoped<IMerchantRepository, MerchantRepository>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddMassTransitTestHarness(configurator => configurator.AddConsumer<UserRegisteredConsumer>());

        Provider = services.BuildServiceProvider(validateScopes: true);

        using (IServiceScope scope = Provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<MerchantsDbContext>().Database.MigrateAsync();
        }

        await Harness.Start();
    }

    public async Task<int> CountMerchantsForOwnerAsync(Guid ownerUserId)
    {
        using IServiceScope scope = Provider.CreateScope();
        MerchantsDbContext context = scope.ServiceProvider.GetRequiredService<MerchantsDbContext>();
        return await context.Merchants.CountAsync(merchant => merchant.OwnerUserId == ownerUserId);
    }

    public async Task DisposeAsync()
    {
        await Harness.Stop();
        await Provider.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

/// <summary>Pruebas del UserRegisteredConsumer (choreography Identity -> Merchants).</summary>
public sealed class UserRegisteredConsumerTests(ConsumerHarnessFixture fixture) : IClassFixture<ConsumerHarnessFixture>
{
    [Fact]
    public async Task Merchant_CreatesSingleDraft_EvenWhenPublishedTwice()
    {
        Guid userId = Guid.CreateVersion7();
        UserRegisteredIntegrationEvent message = MerchantRegistered(userId);

        await fixture.Harness.Bus.Publish(message);
        await fixture.Harness.Bus.Publish(message);

        IConsumerTestHarness<UserRegisteredConsumer> consumer =
            fixture.Provider.GetRequiredService<IConsumerTestHarness<UserRegisteredConsumer>>();
        await WaitForConsumedAsync(consumer, userId, expected: 2);

        (await fixture.CountMerchantsForOwnerAsync(userId)).Should().Be(1);
    }

    [Fact]
    public async Task Customer_DoesNotCreateMerchant()
    {
        Guid userId = Guid.CreateVersion7();
        UserRegisteredIntegrationEvent message = MerchantRegistered(userId) with { UserType = "Customer" };

        await fixture.Harness.Bus.Publish(message);

        IConsumerTestHarness<UserRegisteredConsumer> consumer =
            fixture.Provider.GetRequiredService<IConsumerTestHarness<UserRegisteredConsumer>>();
        await WaitForConsumedAsync(consumer, userId, expected: 1);

        (await fixture.CountMerchantsForOwnerAsync(userId)).Should().Be(0);
    }

    private static UserRegisteredIntegrationEvent MerchantRegistered(Guid userId) => new()
    {
        UserId = userId,
        Email = $"merchant-{userId:N}@rappix.test",
        FirstName = "Meri",
        LastName = "Comercio",
        UserType = "Merchant",
        EmailConfirmed = false,
    };

    private static async Task WaitForConsumedAsync(IConsumerTestHarness<UserRegisteredConsumer> consumer, Guid userId, int expected)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            int consumed = consumer.Consumed
                .Select<UserRegisteredIntegrationEvent>(received => received.Context.Message.UserId == userId)
                .Count();
            if (consumed >= expected)
            {
                return;
            }

            await Task.Delay(100);
        }
    }
}
