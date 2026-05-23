using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Notifications.Application;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Infrastructure.Persistence;
using Rappix.Notifications.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace Rappix.Notifications.Tests.Integration._Shared;

/// <summary>
/// Base de los integration tests de consumers: arranca PostgreSQL via Testcontainers (incluida la
/// migration InitialNotifications con el unique partial index <c>UX_Notification_BusinessKey</c>),
/// registra el grafo de DI (Application + persistence + harness MassTransit), y expone helpers para
/// acceder al harness, al DbContext y al canal mockeado.
/// </summary>
/// <remarks>
/// PostgreSQL es la unica dependencia external — RabbitMQ se sustituye por el harness in-memory
/// (los consumers en Fase 9 solo CONSUMEN del bus + escriben a BD + llaman al canal). El INBOX EF
/// no se prueba aqui via el harness (el harness no usa outbox); ese test critico vive en commit 10
/// (DoubleDeliveryIdempotencyTests) usando real MassTransit + RabbitMQ via WebApplicationFactory.
///
/// Aislamiento per-test (IAsyncLifetime per-class, NO IClassFixture compartido): publishes/consumed
/// acumulan entre tests cuando se comparte un harness, y los tests de proyeccion son state-dependent.
/// Coste extra ~10-15s por test, aceptable por el aislamiento (leccion Dispatch Fase 6).
/// </remarks>
public abstract class NotificationsConsumerTestBase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("rappix_notifications")
        .WithUsername("rappix")
        .WithPassword("rappix_test")
        .Build();

    private ServiceProvider _provider = null!;

    /// <summary>Canal de notificacion mockeado — los tests verifican Channel.Received(...).SendAsync(...).</summary>
    protected INotificationChannel Channel { get; private set; } = null!;

    /// <summary>Harness MassTransit (entrega Bus, Published, Consumed sin RabbitMQ real).</summary>
    protected ITestHarness Harness => _provider.GetRequiredService<ITestHarness>();

    /// <summary>Provider para resolver servicios scoped dentro de cada test.</summary>
    protected IServiceProvider Provider => _provider;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNotificationsApplication();

        // Persistence: equivalente a AddNotificationsInfrastructure SIN AddRappixMessaging (el harness
        // sustituye al broker) y SIN el switch del canal (lo mockeamos).
        services.AddDbContext<NotificationsDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<NotificationsDbContext>());
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IUserContactRepository, UserContactRepository>();
        services.AddScoped<IMerchantContactRepository, MerchantContactRepository>();
        services.AddScoped<INotificationOrderRepository, NotificationOrderRepository>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Canal mockeado — para consumers de proyeccion (commit 6) no se invoca; para consumers de
        // pedido (commits 7-8) los tests verifican Channel.Received(1).SendAsync(...).
        Channel = Substitute.For<INotificationChannel>();
        Channel.SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>())
            .Returns(Rappix.BuildingBlocks.Core.Results.Result.Success(
                new NotificationSendResult(ProviderMessageId: null)));
        services.AddSingleton(Channel);

        // Harness + consumers configurados por la subclase.
        services.AddMassTransitTestHarness(ConfigureHarness);

        _provider = services.BuildServiceProvider(validateScopes: true);

        // Migrar el schema (crea las tablas + el unique partial index UX_Notification_BusinessKey).
        using (IServiceScope scope = _provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
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
    protected (IServiceScope Scope, NotificationsDbContext Db) CreateDbScope()
    {
        IServiceScope scope = _provider.CreateScope();
        return (scope, scope.ServiceProvider.GetRequiredService<NotificationsDbContext>());
    }
}
