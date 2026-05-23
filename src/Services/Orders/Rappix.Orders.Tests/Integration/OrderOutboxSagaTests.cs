using FluentAssertions;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Rappix.Contracts.Orders;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Domain.Common;
using Rappix.Orders.Domain.Orders;
using Rappix.Orders.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Rappix.Orders.Tests.Integration;

/// <summary>
/// Arranca la API de Orders REAL (su Program.cs, su wiring de mensajeria) contra un PostgreSQL y un RabbitMQ
/// efimeros (Testcontainers), con los clientes gRPC mockeados. A diferencia de <c>OrderSagaTests</c> (harness
/// in-memory, sin outbox), aqui corre el <b>outbox transaccional de EF (UseBusOutbox)</b> con su servicio de
/// entrega y el filtro de outbox por endpoint — exactamente la combinacion donde la saga se atascaba en prod.
/// </summary>
public sealed class OrderOutboxSagaFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Imagenes ya presentes en el stack local (sin pull extra). Orders no necesita PostGIS.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("rappix_orders")
        .WithUsername("rappix")
        .WithPassword("rappix_test")
        .Build();

    private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-management-alpine")
        .WithUsername("rappix")
        .WithPassword("rappix_test")
        .Build();

    /// <summary>Cliente gRPC de Pricing (consume/revierte la cotizacion). Default: ambos exitosos.</summary>
    public IPricingClient Pricing { get; } = Substitute.For<IPricingClient>();

    /// <summary>Cliente gRPC de Catalog (reserva stock). Default: la reserva FALLA (camino de compensacion).</summary>
    public IStockReservationClient Stock { get; } = Substitute.For<IStockReservationClient>();

    /// <summary>Repositorio de pedidos. Default: devuelve un pedido con lineas (para que ReserveStock cargue el snapshot).</summary>
    public IOrderRepository Orders { get; } = Substitute.For<IOrderRepository>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Sustituye los clientes gRPC (no se levantan Pricing/Catalog/Merchants). El bus, el outbox, la saga
            // y los consumers "activity" son los REALES; solo se simula el resultado de cada llamada gRPC.
            services.RemoveAll<IPricingClient>();
            services.AddSingleton(Pricing);
            services.RemoveAll<IStockReservationClient>();
            services.AddSingleton(Stock);
            services.RemoveAll<IOrderRepository>();
            services.AddSingleton(Orders);

            Pricing.ConsumeQuoteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(new PricingOperationResult(ServiceAvailable: true, Success: true, string.Empty, "Consumed"));
            Pricing.RevertQuoteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new PricingOperationResult(ServiceAvailable: true, Success: true, string.Empty, "Active"));
            Stock.ReserveAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<StockReservationLineInput>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(new StockOperationResult(ServiceAvailable: true, Success: false, "Catalog.Stock.Insufficient"));
            Orders.GetByIdAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns(_ => FakeOrder());
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _rabbitmq.StartAsync();

        // WebApplication.CreateBuilder lee variables de entorno: se fijan ANTES de construir/arrancar el host.
        Environment.SetEnvironmentVariable("ConnectionStrings__OrdersDb", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("RabbitMq__Host", _rabbitmq.Hostname);
        Environment.SetEnvironmentVariable("RabbitMq__Port", _rabbitmq.GetMappedPublicPort(5672).ToString(System.Globalization.CultureInfo.InvariantCulture));
        Environment.SetEnvironmentVariable("RabbitMq__Username", "rappix");
        Environment.SetEnvironmentVariable("RabbitMq__Password", "rappix_test");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "rappix-test-signing-key-please-change-32bytes-minimum");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "https://localhost:5001");
        Environment.SetEnvironmentVariable("Jwt__Audience", "rappix");
        // Timeouts cortos (TimeSpan) para que el test del disparo real de Quartz no espere minutos. Solo el
        // test de timeout llega a AwaitingMerchant y los usa; el test de outbox falla antes de programar ninguno.
        Environment.SetEnvironmentVariable("Orders__Timeouts__Merchant", "00:00:05");
        Environment.SetEnvironmentVariable("Orders__Timeouts__Payment", "00:00:05");
        Environment.SetEnvironmentVariable("Orders__Timeouts__Courier", "00:00:05");

        // Migra el esquema (Order + saga + inbox/outbox + QRTZ) ANTES de que arranquen los hosted services
        // (bus, entrega del outbox, Quartz), que asumen el esquema presente. Se usa un contexto independiente:
        // tocar la factory aqui arrancaria el host antes de migrar.
        DbContextOptions<OrdersDbContext> options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var migrationContext = new OrdersDbContext(options, Substitute.For<IPublisher>());
        await migrationContext.Database.MigrateAsync();
    }

    /// <summary>Lee el estado persistido de la saga (la proyeccion real del repositorio EF de MassTransit).</summary>
    public async Task<string?> GetSagaStateAsync(Guid orderId)
    {
        using IServiceScope scope = Services.CreateScope();
        OrdersDbContext context = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        return await context.OrderStates.AsNoTracking()
            .Where(state => state.CorrelationId == orderId)
            .Select(state => state.CurrentState)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Arranca el host (al tocar Services) y espera a que el bus este SANO. El hosted service de MassTransit
    /// arranca el bus en segundo plano (WaitUntilStarted=false por defecto), asi que sin esta espera se podria
    /// publicar antes de que la cola de la saga este enlazada al exchange y el broker descartaria el mensaje.
    /// </summary>
    public async Task WaitForBusHealthyAsync(TimeSpan timeout)
    {
        IBusControl busControl = Services.GetRequiredService<IBusControl>();
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            if (busControl.CheckHealth().Status == BusHealthStatus.Healthy)
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException("El bus de MassTransit no alcanzo el estado Healthy dentro del tiempo esperado.");
    }

    /// <summary>Espera (con timeout) a que la saga alcance el estado esperado; devuelve el ultimo estado observado.</summary>
    public async Task<string?> WaitForStateAsync(Guid orderId, string expectedState, TimeSpan timeout)
    {
        string? state = null;
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            state = await GetSagaStateAsync(orderId);
            if (string.Equals(state, expectedState, StringComparison.Ordinal))
            {
                return state;
            }

            await Task.Delay(500);
        }

        return state;
    }

    private static Order FakeOrder()
    {
        OrderLine line = OrderLine.Create(Guid.CreateVersion7(), "Pizza", 100m, 0m, 2).Value;
        DeliveryAddress address = DeliveryAddress.Create("Calle 1", null, 18.48, -69.93).Value;
        return Order.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "Food", "DOP", [line],
            200m, 50m, 0m, 0m, 0m, 0m, 250m, address,
            pickupLatitude: 18.4861, pickupLongitude: -69.9312,
            DateTime.UtcNow).Value;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _rabbitmq.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

/// <summary>
/// Prueba de regresion del bug "la saga se atasca en ValidatingQuote": con <c>UseBusOutbox()</c>, un consumer
/// "activity" puro (que NO escribe en el DbContext) publica su evento-resultado a traves del outbox del bus,
/// que lo bufferiza hasta un SaveChanges que nunca ocurre — salvo que CADA endpoint lleve el filtro de outbox EF
/// (<c>AddConfigureEndpointsCallback(... UseEntityFrameworkOutbox ...)</c>). Sin ese filtro, QuoteConsumed se
/// pierde y la saga jamas sale de ValidatingQuote (sin error ni fault). Esta prueba ejercita el camino real
/// (3 saltos consumer -> saga via outbox: QuoteConsumed, StockReservationFailed, QuoteReverted) y exige que la
/// saga alcance un estado TERMINAL. Si se quita el filtro de outbox del wiring de produccion, esta prueba falla
/// (timeout, atascada en ValidatingQuote). El harness in-memory de OrderSagaTests no puede detectarlo: no usa
/// el bus outbox (y, segun MassTransit, el harness no "olfatea" mensajes outboxed).
/// </summary>
public sealed class OrderOutboxSagaTests(OrderOutboxSagaFactory factory) : IClassFixture<OrderOutboxSagaFactory>
{
    [Fact]
    public async Task ActivityConsumerResults_FlowThroughBusOutbox_SagaReachesTerminalState()
    {
        // Esta prueba necesita que la reserva FALLE (camino de compensacion). Se fija explicitamente porque el
        // test de timeout comparte el mismo substitute y lo pone en exito; xunit no garantiza el orden.
        factory.Stock.ReserveAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<StockReservationLineInput>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new StockOperationResult(ServiceAvailable: true, Success: false, "Catalog.Stock.Insufficient"));

        // Espera a que el bus este sano (todas las colas/bindings listos) antes de publicar.
        await factory.WaitForBusHealthyAsync(TimeSpan.FromSeconds(30));

        IBus bus = factory.Services.GetRequiredService<IBus>();
        Guid orderId = Guid.CreateVersion7();

        await bus.Publish(new OrderSubmittedIntegrationEvent
        {
            OrderId = orderId,
            CustomerUserId = Guid.CreateVersion7(),
            MerchantId = Guid.CreateVersion7(),
            QuoteId = Guid.CreateVersion7(),
            TotalAmount = 250m,
            Currency = "DOP",
            DeliveryAddress = "Calle 1",
            DeliveryLatitude = 18.48,
            DeliveryLongitude = -69.93,
            PickupLatitude = 18.4861,
            PickupLongitude = -69.9312,
        });

        // El stub de stock falla, asi que el camino feliz se desvia a compensacion: ValidatingQuote ->
        // (QuoteConsumed) -> ReservingStock -> (StockReservationFailed) -> CompensatingQuote ->
        // (QuoteReverted) -> Failed. Los tres eventos-resultado viajan por el bus outbox.
        string? finalState = await factory.WaitForStateAsync(orderId, expectedState: "Failed", timeout: TimeSpan.FromSeconds(60));

        finalState.Should().Be("Failed",
            "los eventos-resultado de los consumers activity deben fluir por el outbox del bus; si no, la saga se queda en ValidatingQuote");

        // Los tres saltos consumer -> saga ocurrieron de verdad (no quedo atascada en el primero).
        await Pricing.Received().ConsumeQuoteAsync(Arg.Any<Guid>(), orderId, Arg.Any<CancellationToken>());
        await Stock.Received().ReserveAsync(orderId, Arg.Any<IReadOnlyList<StockReservationLineInput>>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await Pricing.Received().RevertQuoteAsync(Arg.Any<Guid>(), orderId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifica que un timeout de la saga programado en <b>Quartz + Postgres REALES</b> efectivamente DISPARA y
    /// avanza la saga. El harness in-memory de <c>OrderSagaTests</c> usa un scheduler virtual que nunca toca
    /// <c>qrtz_triggers</c> ni la conversion de tiempo del job store, por eso "pasa" alli aunque fallara en
    /// runtime. Camino: el pedido llega a AwaitingMerchant, nadie acepta, y el timeout corto (5s) dispara la
    /// compensacion (release stock -> revert quote) hasta Cancelled.
    /// </summary>
    [Fact]
    public async Task MerchantTimeout_FiresViaRealQuartz_CancelsSaga()
    {
        // Camino feliz hasta AwaitingMerchant: reserva con exito; la liberacion (compensacion) tambien.
        factory.Stock.ReserveAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<StockReservationLineInput>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new StockOperationResult(ServiceAvailable: true, Success: true, string.Empty));
        factory.Stock.ReleaseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new StockOperationResult(ServiceAvailable: true, Success: true, string.Empty));

        await factory.WaitForBusHealthyAsync(TimeSpan.FromSeconds(30));

        IBus bus = factory.Services.GetRequiredService<IBus>();
        Guid orderId = Guid.CreateVersion7();

        await bus.Publish(new OrderSubmittedIntegrationEvent
        {
            OrderId = orderId,
            CustomerUserId = Guid.CreateVersion7(),
            MerchantId = Guid.CreateVersion7(),
            QuoteId = Guid.CreateVersion7(),
            TotalAmount = 250m,
            Currency = "DOP",
            DeliveryAddress = "Calle 1",
            DeliveryLatitude = 18.48,
            DeliveryLongitude = -69.93,
            PickupLatitude = 18.4861,
            PickupLongitude = -69.9312,
        });

        // Nadie acepta: el timeout de merchant (5s, en Quartz+Postgres real) debe disparar -> compensacion -> Cancelled.
        string? finalState = await factory.WaitForStateAsync(orderId, expectedState: "Cancelled", timeout: TimeSpan.FromSeconds(60));

        finalState.Should().Be("Cancelled",
            "el timeout de merchant programado en Quartz debe disparar y cancelar el pedido no aceptado");

        // Llego a AwaitingMerchant (consumio quote + reservo stock) y el timeout disparo la compensacion completa.
        await Pricing.Received().ConsumeQuoteAsync(Arg.Any<Guid>(), orderId, Arg.Any<CancellationToken>());
        await Stock.Received().ReserveAsync(orderId, Arg.Any<IReadOnlyList<StockReservationLineInput>>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await Stock.Received().ReleaseAsync(orderId, Arg.Any<CancellationToken>());
        await Pricing.Received().RevertQuoteAsync(Arg.Any<Guid>(), orderId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private IPricingClient Pricing => factory.Pricing;
    private IStockReservationClient Stock => factory.Stock;
}
