using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Tracking.Application;
using Rappix.Tracking.Application.Abstractions;
using Rappix.Tracking.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Rappix.Tracking.Tests.Integration._Shared;

/// <summary>
/// Base de las pruebas integration de los consumers: arranca PostgreSQL via Testcontainers, registra
/// el grafo de DI (Application + persistence + harness MassTransit), migra el schema y entrega
/// helpers para acceder al harness, al DbContext y al notifier mock. PostgreSQL es la unica
/// dependencia external — RabbitMQ se sustituye por el harness in-memory (los consumers en Fase 7
/// solo CONSUMEN, no publican al bus, asi que el harness es suficiente).
/// </summary>
/// <remarks>
/// Cada subclase de test usa <see cref="IAsyncLifetime"/> per-class, no <c>IClassFixture</c>:
/// publishes/consumed acumulan entre tests cuando se comparte un harness, y los tests de proyeccion
/// son state-dependent. El coste extra (~10-15s por test) es aceptable para garantizar aislamiento.
/// </remarks>
public abstract class TrackingConsumerTestBase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("rappix_tracking")
        .WithUsername("rappix")
        .WithPassword("rappix_test")
        .Build();

    private ServiceProvider _provider = null!;

    /// <summary>Notifier SignalR mockeado — los tests verifican Received.PushXxx(...) sin hub real.</summary>
    protected IClientNotifier Notifier { get; private set; } = null!;

    /// <summary>Harness MassTransit (entrega Bus, Published, Consumed sin RabbitMQ).</summary>
    protected ITestHarness Harness => _provider.GetRequiredService<ITestHarness>();

    /// <summary>Provider para resolver servicios scoped dentro de cada test.</summary>
    protected IServiceProvider Provider => _provider;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTrackingApplication();

        // Persistence: igual que AddTrackingInfrastructure pero sin AddRappixMessaging — el
        // harness sustituye al broker.
        services.AddDbContext<TrackingDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<TrackingDbContext>());
        services.AddScoped<IOrderTrackingReadRepository, OrderTrackingReadRepository>();
        services.AddScoped<IOrderTrackingRepository, OrderTrackingRepository>();
        services.AddScoped<ICourierActiveOrderRepository, CourierActiveOrderRepository>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Notifier SignalR sustituido — los tests verifican llamadas sin necesidad de levantar hub.
        Notifier = Substitute.For<IClientNotifier>();
        services.AddSingleton(Notifier);

        // Harness + consumers configurados por la subclase.
        services.AddMassTransitTestHarness(ConfigureHarness);

        _provider = services.BuildServiceProvider(validateScopes: true);

        using (IServiceScope scope = _provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TrackingDbContext>().Database.MigrateAsync();
        }

        await Harness.Start();
    }

    public async Task DisposeAsync()
    {
        await Harness.Stop();
        await _provider.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>La subclase declara que consumers registrar (uno por test class para minimizar ruido).</summary>
    protected abstract void ConfigureHarness(IBusRegistrationConfigurator configurator);

    /// <summary>Crea un scope y devuelve un DbContext fresco — usar en asserts post-Consume.</summary>
    protected (IServiceScope Scope, TrackingDbContext Db) CreateDbScope()
    {
        IServiceScope scope = _provider.CreateScope();
        return (scope, scope.ServiceProvider.GetRequiredService<TrackingDbContext>());
    }
}
