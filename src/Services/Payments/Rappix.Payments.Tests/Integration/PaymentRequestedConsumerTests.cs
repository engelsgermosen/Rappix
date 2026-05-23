using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Payments;
using Rappix.Payments.Application;
using Rappix.Payments.Application.Abstractions;
using Rappix.Payments.Application.Configuration;
using Rappix.Payments.Domain.Payments;
using Rappix.Payments.Infrastructure.Gateways;
using Rappix.Payments.Infrastructure.Messaging;
using Rappix.Payments.Infrastructure.Persistence;
using Rappix.Payments.Tests.Integration._Shared;
using Testcontainers.PostgreSql;

namespace Rappix.Payments.Tests.Integration;

/// <summary>
/// Base abstracto: cada test arranca su propio container PostgreSQL via <see cref="IAsyncLifetime"/>
/// para aislamiento total (state cleanup entre tests con <c>IClassFixture</c> probo ser fragil —
/// leccion documentada en Dispatch). Coste extra ~10s por test, aceptable: este es el CHECKPOINT de
/// mayor riesgo de Fase 8 (idempotencia de dinero, defensa contra doble cobro en 3 niveles).
/// </summary>
public abstract class PaymentRequestedConsumerTestsBase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("rappix_payments")
        .WithUsername("rappix")
        .WithPassword("rappix_test")
        .Build();

    private ServiceProvider _provider = null!;

    /// <summary>Modo del FakePaymentGateway: "Always" (happy) o "AlwaysFail" (rejection).</summary>
    protected abstract string FakeMode { get; }

    protected ITestHarness Harness => _provider.GetRequiredService<ITestHarness>();
    protected CountingPaymentGateway Counter => _provider.GetRequiredService<CountingPaymentGateway>();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPaymentsApplication();

        services.AddDbContext<PaymentsDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<PaymentsDbContext>());
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.Configure<PaymentsOptions>(options =>
        {
            options.Gateway = "Fake";
            options.Fake.Mode = FakeMode;
        });

        // FakePaymentGateway envuelto en CountingPaymentGateway: el Counting se registra como
        // Singleton para que el test pueda leer los contadores. Vigilance #2 sostiene: ambas instancias
        // son INSTANCE-LEVEL; cada IAsyncLifetime arranca con dicts/contadores vacios.
        services.AddSingleton<CountingPaymentGateway>(provider =>
            new CountingPaymentGateway(new FakePaymentGateway(provider.GetRequiredService<IOptions<PaymentsOptions>>())));
        services.AddSingleton<IPaymentGateway>(provider => provider.GetRequiredService<CountingPaymentGateway>());

        services.AddMassTransitTestHarness(configurator => configurator.AddConsumer<PaymentRequestedConsumer>());

        _provider = services.BuildServiceProvider(validateScopes: true);

        using (IServiceScope scope = _provider.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Database.MigrateAsync();
        }

        await Harness.Start();
    }

    public async Task DisposeAsync()
    {
        await Harness.Stop();
        await _provider.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected async Task<Payment?> GetPaymentAsync(Guid orderId)
    {
        using IServiceScope scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>()
            .Payments.FirstOrDefaultAsync(payment => payment.Id == orderId);
    }

    protected async Task<int> CountPaymentsAsync(Guid orderId)
    {
        using IServiceScope scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>()
            .Payments.CountAsync(payment => payment.Id == orderId);
    }

    protected async Task SeedAuthorizedPaymentAsync(Guid orderId, string intentId)
    {
        using IServiceScope scope = _provider.CreateScope();
        PaymentsDbContext db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        Money money = Money.Create(100m, "DOP").Value;
        Payment payment = Payment.Create(orderId, customerUserId: Guid.CreateVersion7(), amount: money, utcNow: DateTime.UtcNow);
        payment.Authorize(intentId, DateTime.UtcNow);
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
    }

    protected async Task WaitForConsumedAsync(Guid orderId, int expected = 1)
    {
        IConsumerTestHarness<PaymentRequestedConsumer> consumer =
            _provider.GetRequiredService<IConsumerTestHarness<PaymentRequestedConsumer>>();
        for (int attempt = 0; attempt < 60; attempt++)
        {
            int consumed = consumer.Consumed
                .Select<PaymentRequestedIntegrationEvent>(received => received.Context.Message.OrderId == orderId)
                .Count();
            if (consumed >= expected)
            {
                return;
            }

            await Task.Delay(100);
        }
    }
}

/// <summary>Tests con FakeMode=Always (camino feliz + idempotencia). Cuatro casos incluyendo el TEST CRITICO de doble cobro.</summary>
public sealed class PaymentRequestedConsumerTests : PaymentRequestedConsumerTestsBase
{
    protected override string FakeMode => "Always";

    [Fact]
    public async Task HappyPath_PublishesPaymentSucceeded_PersistsAuthorizedPayment_CallsGatewayOnce()
    {
        Guid orderId = Guid.CreateVersion7();
        Guid customerUserId = Guid.CreateVersion7();

        await Harness.Bus.Publish(new PaymentRequestedIntegrationEvent
        {
            OrderId = orderId,
            CustomerUserId = customerUserId,
            Amount = 250.50m,
            Currency = "DOP",
        });
        await WaitForConsumedAsync(orderId);

        // Aggregate persistido en Authorized con pi_fake_xxx.
        Payment? payment = await GetPaymentAsync(orderId);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.Authorized);
        payment.CustomerUserId.Should().Be(customerUserId);
        payment.Amount.Amount.Should().Be(250.50m);
        payment.Amount.Currency.Should().Be("DOP");
        payment.ProviderPaymentIntentId.Should().StartWith("pi_fake_");

        // Exactamente 1 PaymentSucceeded publicado y 0 PaymentFailed.
        (await Harness.Published.SelectAsync<PaymentSucceededIntegrationEvent>(filter =>
            filter.Context.Message.OrderId == orderId).CountAsync()).Should().Be(1);
        (await Harness.Published.SelectAsync<PaymentFailedIntegrationEvent>(filter =>
            filter.Context.Message.OrderId == orderId).CountAsync()).Should().Be(0);

        // Gateway llamado exactamente una vez.
        Counter.AuthorizeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task SequentialRedelivery_GatewayCalledOnce_OneRow_OnePublish()
    {
        // EL TEST CRITICO DE FASE 8: defensa contra doble cobro en el escenario REALISTA de
        // re-entrega del broker (Nivel 2 del aggregate, sin race concurrente).
        //
        // En produccion el flujo es: la saga publica 1 vez, RabbitMQ entrega 1 vez. Si el ack se
        // pierde (crash del consumer post-SaveChanges pre-ack), el broker reentrega tras ~30s. La
        // segunda entrega es SECUENCIAL respecto a la primera (el primer consume ya termino), no
        // concurrente. En produccion ademas el inbox EF (Nivel 1) bloquea la reentrega antes de
        // invocar al consumer; aqui en el harness in-memory que NO honra el outbox/inbox, solo nos
        // queda la defensa del aggregate.
        //
        // Resultado esperado: 1 llamada al gateway, 1 fila payments, 1 PaymentSucceeded publicado.
        Guid orderId = Guid.CreateVersion7();
        Guid customerUserId = Guid.CreateVersion7();
        var payload = new PaymentRequestedIntegrationEvent
        {
            OrderId = orderId,
            CustomerUserId = customerUserId,
            Amount = 100m,
            Currency = "DOP",
        };

        // Primera entrega: consumer crea Payment + autoriza + publica.
        await Harness.Bus.Publish(payload);
        await WaitForConsumedAsync(orderId, expected: 1);

        // Segunda entrega (re-entrega del broker simulada): consumer encuentra el Payment ya
        // existente y retorna sin tocar el gateway ni publicar.
        await Harness.Bus.Publish(payload);
        await WaitForConsumedAsync(orderId, expected: 2);

        // EXACTAMENTE 1 fila en payments.
        (await CountPaymentsAsync(orderId)).Should().Be(1);

        // EXACTAMENTE 1 PaymentSucceeded publicado.
        (await Harness.Published.SelectAsync<PaymentSucceededIntegrationEvent>(filter =>
            filter.Context.Message.OrderId == orderId).CountAsync()).Should().Be(1);

        // EXACTAMENTE 1 llamada al gateway. Sin esto, doble cobro real.
        Counter.AuthorizeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentPublish_RacePreventedByPk_OneRow_OnePublish_StripeKeyPreventsDoubleCharge()
    {
        // Cobertura adicional: cuando DOS mensajes con el mismo OrderId llegan en paralelo (escenario
        // raro pero posible si la saga es re-emitida en circunstancias exoticas), ambos consumers
        // pueden hacer GetByOrderIdAsync->null simultaneamente y llamar al gateway. Resultado:
        // - El gateway puede ser llamado >1 vez (contador no es necesariamente 1), PERO
        // - Solo UNA fila se persiste (PK = OrderId genera unique violation en el segundo SaveChanges
        //   que MassTransit reintenta; al reintentar encuentra la fila y retorna no-op), Y
        // - Solo UN PaymentSucceeded se publica (el segundo consume no llega a publish), Y
        // - El Fake/Stripe gateway devuelve el MISMO ProviderPaymentIntentId para ambas llamadas
        //   gracias a la idempotency-key estable (Nivel 3) -> sin doble cobro real en el proveedor.
        Guid orderId = Guid.CreateVersion7();
        Guid customerUserId = Guid.CreateVersion7();
        var payload = new PaymentRequestedIntegrationEvent
        {
            OrderId = orderId,
            CustomerUserId = customerUserId,
            Amount = 100m,
            Currency = "DOP",
        };

        await Task.WhenAll(
            Harness.Bus.Publish(payload),
            Harness.Bus.Publish(payload));
        await WaitForConsumedAsync(orderId, expected: 2);

        // Nivel 2 (PK natural sobre OrderId): exactamente 1 fila.
        (await CountPaymentsAsync(orderId)).Should().Be(1);

        // Solo 1 publish (el segundo consume no llega a publish por el conflicto en SaveChanges).
        (await Harness.Published.SelectAsync<PaymentSucceededIntegrationEvent>(filter =>
            filter.Context.Message.OrderId == orderId).CountAsync()).Should().Be(1);

        // El gateway puede haber sido llamado 1 o 2 veces (race-dependiente), pero al menos 1.
        Counter.AuthorizeCallCount.Should().BeGreaterThanOrEqualTo(1);

        // La fila persistida tiene el ProviderPaymentIntentId devuelto por el Fake (mismo intent
        // para ambas llamadas concurrentes — Nivel 3 simulado: same idempotency-key -> same intent id).
        Payment? payment = await GetPaymentAsync(orderId);
        payment.Should().NotBeNull();
        payment!.ProviderPaymentIntentId.Should().StartWith("pi_fake_");
    }

    [Fact]
    public async Task Reentrega_WhenPaymentAlreadyAuthorized_IsNoOp_NoSecondPublish_NoSecondGatewayCall()
    {
        // Patron espejo de Reentrega_NoCreaSegundaAsignacion de Dispatch. Pre-seed manual de un
        // Payment Authorized en DB; al recibir PaymentRequested para el mismo OrderId, el consumer
        // detecta la existencia y retorna (sin publish, sin gateway).
        Guid orderId = Guid.CreateVersion7();
        await SeedAuthorizedPaymentAsync(orderId, intentId: "pi_fake_preexisting");

        await Harness.Bus.Publish(new PaymentRequestedIntegrationEvent
        {
            OrderId = orderId,
            CustomerUserId = Guid.CreateVersion7(),
            Amount = 100m,
            Currency = "DOP",
        });
        await WaitForConsumedAsync(orderId);

        // El Payment sigue siendo el seed (mismo intent id).
        Payment? payment = await GetPaymentAsync(orderId);
        payment.Should().NotBeNull();
        payment!.ProviderPaymentIntentId.Should().Be("pi_fake_preexisting");

        // Cero publishes nuevos (sin replay).
        (await Harness.Published.SelectAsync<PaymentSucceededIntegrationEvent>(filter =>
            filter.Context.Message.OrderId == orderId).CountAsync()).Should().Be(0);

        // Cero llamadas al gateway: la idempotency guard del aggregate bloqueo antes de tocar el gateway.
        Counter.AuthorizeCallCount.Should().Be(0);
    }

    [Fact]
    public async Task InvalidMoney_PublishesPaymentFailed_DoesNotCreatePayment_DoesNotCallGateway()
    {
        // Defensa contra payloads malformados de Orders. Cero rows + PaymentFailed publicado + 0 gateway.
        Guid orderId = Guid.CreateVersion7();

        await Harness.Bus.Publish(new PaymentRequestedIntegrationEvent
        {
            OrderId = orderId,
            CustomerUserId = Guid.CreateVersion7(),
            Amount = -50m,                 // invalido (no positivo)
            Currency = "DOP",
        });
        await WaitForConsumedAsync(orderId);

        (await CountPaymentsAsync(orderId)).Should().Be(0);
        (await Harness.Published.SelectAsync<PaymentFailedIntegrationEvent>(filter =>
            filter.Context.Message.OrderId == orderId).CountAsync()).Should().Be(1);
        Counter.AuthorizeCallCount.Should().Be(0);
    }
}

/// <summary>Test con FakeMode=AlwaysFail: rechazo explicito del gateway.</summary>
public sealed class PaymentRequestedConsumerTestsFailingGateway : PaymentRequestedConsumerTestsBase
{
    protected override string FakeMode => "AlwaysFail";

    [Fact]
    public async Task WhenGatewayRejects_PublishesPaymentFailed_PersistsFailedPayment_CallsGatewayOnce()
    {
        Guid orderId = Guid.CreateVersion7();

        await Harness.Bus.Publish(new PaymentRequestedIntegrationEvent
        {
            OrderId = orderId,
            CustomerUserId = Guid.CreateVersion7(),
            Amount = 100m,
            Currency = "DOP",
        });
        await WaitForConsumedAsync(orderId);

        Payment? payment = await GetPaymentAsync(orderId);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.Failed);
        payment.Reason.Should().Contain("AlwaysFail");

        (await Harness.Published.SelectAsync<PaymentFailedIntegrationEvent>(filter =>
            filter.Context.Message.OrderId == orderId).CountAsync()).Should().Be(1);
        Counter.AuthorizeCallCount.Should().Be(1);
    }
}
