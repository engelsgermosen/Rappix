using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Identity;
using Rappix.Dispatch.Application;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Domain.Couriers;
using Rappix.Dispatch.Infrastructure.Messaging;
using Rappix.Dispatch.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Rappix.Dispatch.Tests.Integration;

/// <summary>
/// Arranca el consumer en un harness de MassTransit in-memory contra un PostgreSQL real, con
/// MediatR, los repositorios y el DbContext reales (sin RabbitMQ ni WebApplicationFactory).
/// </summary>
public sealed class DispatchConsumerHarnessFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("rappix_dispatch")
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
        services.AddDispatchApplication();
        services.AddDbContext<DispatchDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<DispatchDbContext>());
        services.AddScoped<ICourierRepository, CourierRepository>();
        services.AddScoped<ICourierAssignmentRepository, CourierAssignmentRepository>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddMassTransitTestHarness(configurator => configurator.AddConsumer<UserRegisteredConsumer>());

        Provider = services.BuildServiceProvider(validateScopes: true);

        using (IServiceScope scope = Provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DispatchDbContext>().Database.MigrateAsync();
        }

        await Harness.Start();
    }

    public async Task<int> CountCouriersAsync(Guid userId)
    {
        using IServiceScope scope = Provider.CreateScope();
        DispatchDbContext context = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();
        return await context.CourierProfiles.CountAsync(courier => courier.Id == CourierId.FromUserId(userId));
    }

    public async Task DisposeAsync()
    {
        await Harness.Stop();
        await Provider.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

/// <summary>Pruebas del UserRegisteredConsumer (choreography Identity -> Dispatch).</summary>
public sealed class UserRegisteredConsumerTests(DispatchConsumerHarnessFixture fixture) : IClassFixture<DispatchConsumerHarnessFixture>
{
    [Fact]
    public async Task Courier_CreatesSingleDraft_EvenWhenPublishedTwice()
    {
        Guid userId = Guid.CreateVersion7();
        UserRegisteredIntegrationEvent message = CourierRegistered(userId);

        await fixture.Harness.Bus.Publish(message);
        await fixture.Harness.Bus.Publish(message);

        IConsumerTestHarness<UserRegisteredConsumer> consumer =
            fixture.Provider.GetRequiredService<IConsumerTestHarness<UserRegisteredConsumer>>();
        await WaitForConsumedAsync(consumer, userId, expected: 2);

        (await fixture.CountCouriersAsync(userId)).Should().Be(1);
    }

    [Fact]
    public async Task Customer_DoesNotCreateCourier()
    {
        Guid userId = Guid.CreateVersion7();
        UserRegisteredIntegrationEvent message = CourierRegistered(userId) with { UserType = "Customer" };

        await fixture.Harness.Bus.Publish(message);

        IConsumerTestHarness<UserRegisteredConsumer> consumer =
            fixture.Provider.GetRequiredService<IConsumerTestHarness<UserRegisteredConsumer>>();
        await WaitForConsumedAsync(consumer, userId, expected: 1);

        (await fixture.CountCouriersAsync(userId)).Should().Be(0);
    }

    [Fact]
    public async Task Merchant_DoesNotCreateCourier()
    {
        // El consumer de Merchants es el que reacciona a UserType=Merchant; el de Dispatch lo ignora.
        Guid userId = Guid.CreateVersion7();
        UserRegisteredIntegrationEvent message = CourierRegistered(userId) with { UserType = "Merchant" };

        await fixture.Harness.Bus.Publish(message);

        IConsumerTestHarness<UserRegisteredConsumer> consumer =
            fixture.Provider.GetRequiredService<IConsumerTestHarness<UserRegisteredConsumer>>();
        await WaitForConsumedAsync(consumer, userId, expected: 1);

        (await fixture.CountCouriersAsync(userId)).Should().Be(0);
    }

    private static UserRegisteredIntegrationEvent CourierRegistered(Guid userId) => new()
    {
        UserId = userId,
        Email = $"courier-{userId:N}@rappix.test",
        FirstName = "Coco",
        LastName = "Reparto",
        UserType = "Courier",
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
