using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Orders;
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
/// Pruebas del <see cref="OrderTerminalCompensationConsumer"/>. Cubre las 3 rutas:
/// <list type="bullet">
/// <item><b>OrderCancelled</b>: Authorized -&gt; Void; Captured -&gt; NeedsReview (no auto-refund).</item>
/// <item><b>OrderFailed</b>: idem (mismo CompensateAsync).</item>
/// <item><b>RefundRequested</b>: Captured -&gt; Refund + publish RefundCompleted; otros estados no-op.</item>
/// </list>
/// El test critico de esta clase es <c>Captured_PlusRefundRequested_RefundsAndPublishesCompleted</c>:
/// VIGILANCE ITEM #3 del plan — asserta DOS capas (DB Status==Refunded + Harness.Published==1)
/// para guardar contra el bug #7 de Orders (publish sin SaveChanges previo se queda bufferizado).
/// </summary>
public sealed class OrderTerminalCompensationConsumerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("rappix_payments")
        .WithUsername("rappix")
        .WithPassword("rappix_test")
        .Build();

    private ServiceProvider _provider = null!;

    private ITestHarness Harness => _provider.GetRequiredService<ITestHarness>();
    private CountingPaymentGateway Counter => _provider.GetRequiredService<CountingPaymentGateway>();

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
        services.Configure<PaymentsOptions>(options => { options.Gateway = "Fake"; options.Fake.Mode = "Always"; });
        services.AddSingleton<CountingPaymentGateway>(provider =>
            new CountingPaymentGateway(new FakePaymentGateway(provider.GetRequiredService<IOptions<PaymentsOptions>>())));
        services.AddSingleton<IPaymentGateway>(provider => provider.GetRequiredService<CountingPaymentGateway>());
        services.AddMassTransitTestHarness(configurator => configurator.AddConsumer<OrderTerminalCompensationConsumer>());

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

    // -------- OrderCancelled --------

    [Fact]
    public async Task Authorized_PlusOrderCancelled_VoidsHold_GatewayVoidCalledOnce()
    {
        Guid orderId = Guid.CreateVersion7();
        await SeedPaymentInStatusAsync(orderId, PaymentStatus.Authorized, intentId: "pi_fake_001");

        await Harness.Bus.Publish(new OrderCancelledIntegrationEvent
        {
            OrderId = orderId,
            Reason = "merchant rejected",
            CancelledAtUtc = DateTime.UtcNow,
        });
        await WaitForOrderCancelledConsumedAsync(orderId);

        Payment? payment = await GetPaymentAsync(orderId);
        payment!.Status.Should().Be(PaymentStatus.Voided);
        payment.Reason.Should().Be("merchant rejected");

        Counter.VoidCallCount.Should().Be(1);
        Counter.RefundCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Captured_PlusOrderCancelled_MarksNeedsReview_DoesNotCallGateway()
    {
        // POLITICA CRITICA: dinero ya capturado + compensacion -> NeedsReview (humano decide). NO
        // auto-refund. NO llamamos al gateway. Consistente con StockCommitFails_NeedsReview de saga.
        Guid orderId = Guid.CreateVersion7();
        await SeedPaymentInStatusAsync(orderId, PaymentStatus.Captured, intentId: "pi_fake_002");

        await Harness.Bus.Publish(new OrderCancelledIntegrationEvent
        {
            OrderId = orderId,
            Reason = "late cancel post-capture",
            CancelledAtUtc = DateTime.UtcNow,
        });
        await WaitForOrderCancelledConsumedAsync(orderId);

        Payment? payment = await GetPaymentAsync(orderId);
        payment!.Status.Should().Be(PaymentStatus.NeedsReview);
        payment.Reason.Should().Be("late cancel post-capture");

        Counter.VoidCallCount.Should().Be(0);
        Counter.RefundCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Pending_PlusOrderCancelled_NoOp_KeepsPendingStatus()
    {
        // Cancelacion antes de Authorize: el aggregate sigue Pending. No tocamos el gateway.
        Guid orderId = Guid.CreateVersion7();
        await SeedPaymentInStatusAsync(orderId, PaymentStatus.Pending, intentId: null);

        await Harness.Bus.Publish(new OrderCancelledIntegrationEvent
        {
            OrderId = orderId,
            Reason = "early cancel",
            CancelledAtUtc = DateTime.UtcNow,
        });
        await WaitForOrderCancelledConsumedAsync(orderId);

        Payment? payment = await GetPaymentAsync(orderId);
        payment!.Status.Should().Be(PaymentStatus.Pending);
        Counter.VoidCallCount.Should().Be(0);
    }

    [Fact]
    public async Task NoPayment_PlusOrderCancelled_NoOp_DoesNotCallGateway()
    {
        // Cancelacion pre-AwaitingPayment (saga rechazada por merchant antes de pago): no hay
        // aggregate; defensa silenciosa.
        Guid orderId = Guid.CreateVersion7();

        await Harness.Bus.Publish(new OrderCancelledIntegrationEvent
        {
            OrderId = orderId,
            Reason = "early reject",
            CancelledAtUtc = DateTime.UtcNow,
        });
        await WaitForOrderCancelledConsumedAsync(orderId);

        (await CountPaymentsAsync(orderId)).Should().Be(0);
        Counter.VoidCallCount.Should().Be(0);
    }

    // -------- OrderFailed --------

    [Fact]
    public async Task Authorized_PlusOrderFailed_VoidsHold_GatewayVoidCalledOnce()
    {
        Guid orderId = Guid.CreateVersion7();
        await SeedPaymentInStatusAsync(orderId, PaymentStatus.Authorized, intentId: "pi_fake_003");

        await Harness.Bus.Publish(new OrderFailedIntegrationEvent
        {
            OrderId = orderId,
            Reason = "stock commit failed",
            FailedAtUtc = DateTime.UtcNow,
        });
        await WaitForOrderFailedConsumedAsync(orderId);

        Payment? payment = await GetPaymentAsync(orderId);
        payment!.Status.Should().Be(PaymentStatus.Voided);
        Counter.VoidCallCount.Should().Be(1);
    }

    // -------- RefundRequested (VIGILANCE ITEM #3) --------

    [Fact]
    public async Task Captured_PlusRefundRequested_RefundsAndPublishesCompleted()
    {
        // VIGILANCE ITEM #3 del plan: este test asserta DOS capas para guardar contra el bug #7 de
        // Orders (publish sin SaveChanges previo se queda bufferizado al outbox y nunca llega al broker):
        //
        //   1) DB layer: Status == Refunded (SaveChanges OK).
        //   2) Outbox/publish layer: RefundCompletedIntegrationEvent publicado (1 vez).
        //
        // Si nos olvidaramos el SaveChanges, en el harness in-memory el publish todavia se emite
        // (porque MassTransit no usa el outbox aqui), pero el aggregate quedaria en Captured -> test
        // falla en la assert #1. Esa es la capa que sostiene el guardrail aqui.
        // En produccion, el AddConfigureEndpointsCallback con UseEntityFrameworkOutbox<PaymentsDbContext>
        // garantiza que el publish entra en la misma transaccion del SaveChanges (defensa real
        // contra el bug #7).
        Guid orderId = Guid.CreateVersion7();
        await SeedPaymentInStatusAsync(orderId, PaymentStatus.Captured, intentId: "pi_fake_004");

        await Harness.Bus.Publish(new RefundRequestedIntegrationEvent
        {
            OrderId = orderId,
            Amount = 100m,
            Currency = "DOP",
            Reason = "saga compensation post-capture",
        });
        await WaitForRefundRequestedConsumedAsync(orderId);

        // Capa 1 (DB): Status == Refunded, ProviderRefundId persistido.
        Payment? payment = await GetPaymentAsync(orderId);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.Refunded);
        payment.ProviderRefundId.Should().StartWith("re_fake_");

        // Capa 2 (publish via outbox-flush guardrail): exactamente 1 RefundCompleted publicado.
        (await Harness.Published.SelectAsync<RefundCompletedIntegrationEvent>(filter =>
            filter.Context.Message.OrderId == orderId).CountAsync()).Should().Be(1);

        // Gateway llamado exactamente una vez en RefundAsync.
        Counter.RefundCallCount.Should().Be(1);
        Counter.VoidCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Authorized_PlusRefundRequested_NoOp_DoesNotRefund()
    {
        // Caso esperado por la saga cuando hay race PaymentSucceeded tardio post-cancelacion (lineas
        // 159/167 de OrderStateMachine.cs): la saga emite RefundRequested pero el aggregate puede
        // estar todavia en Authorized (no se capturo). Politica: Payments es la fuente de verdad;
        // si no esta Captured, no hay nada que refundar (el Void ya fue manejado por OrderCancelled).
        Guid orderId = Guid.CreateVersion7();
        await SeedPaymentInStatusAsync(orderId, PaymentStatus.Authorized, intentId: "pi_fake_005");

        await Harness.Bus.Publish(new RefundRequestedIntegrationEvent
        {
            OrderId = orderId,
            Amount = 100m,
            Currency = "DOP",
            Reason = "race condition",
        });
        await WaitForRefundRequestedConsumedAsync(orderId);

        Payment? payment = await GetPaymentAsync(orderId);
        payment!.Status.Should().Be(PaymentStatus.Authorized); // sin cambio
        Counter.RefundCallCount.Should().Be(0);

        (await Harness.Published.SelectAsync<RefundCompletedIntegrationEvent>(filter =>
            filter.Context.Message.OrderId == orderId).CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Refunded_PlusRefundRequested_IsIdempotent_NoOp_NoSecondGatewayCall()
    {
        // Re-entrega de RefundRequested: el aggregate ya esta Refunded -> no-op silencioso.
        Guid orderId = Guid.CreateVersion7();
        await SeedPaymentInStatusAsync(orderId, PaymentStatus.Refunded, intentId: "pi_fake_006", refundId: "re_fake_existing");

        await Harness.Bus.Publish(new RefundRequestedIntegrationEvent
        {
            OrderId = orderId,
            Amount = 100m,
            Currency = "DOP",
            Reason = "retry",
        });
        await WaitForRefundRequestedConsumedAsync(orderId);

        Payment? payment = await GetPaymentAsync(orderId);
        payment!.Status.Should().Be(PaymentStatus.Refunded);
        payment.ProviderRefundId.Should().Be("re_fake_existing");

        Counter.RefundCallCount.Should().Be(0);
        (await Harness.Published.SelectAsync<RefundCompletedIntegrationEvent>(filter =>
            filter.Context.Message.OrderId == orderId).CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task NoPayment_PlusRefundRequested_NoOp_DoesNotCallGateway()
    {
        Guid orderId = Guid.CreateVersion7();

        await Harness.Bus.Publish(new RefundRequestedIntegrationEvent
        {
            OrderId = orderId,
            Amount = 100m,
            Currency = "DOP",
            Reason = "no payment",
        });
        await WaitForRefundRequestedConsumedAsync(orderId);

        (await CountPaymentsAsync(orderId)).Should().Be(0);
        Counter.RefundCallCount.Should().Be(0);
    }

    // -------- Helpers --------

    private async Task<Payment?> GetPaymentAsync(Guid orderId)
    {
        using IServiceScope scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>()
            .Payments.FirstOrDefaultAsync(payment => payment.Id == orderId);
    }

    private async Task<int> CountPaymentsAsync(Guid orderId)
    {
        using IServiceScope scope = _provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>()
            .Payments.CountAsync(payment => payment.Id == orderId);
    }

    private async Task SeedPaymentInStatusAsync(Guid orderId, PaymentStatus targetStatus, string? intentId, string? refundId = null)
    {
        using IServiceScope scope = _provider.CreateScope();
        PaymentsDbContext db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        Money money = Money.Create(100m, "DOP").Value;
        DateTime now = DateTime.UtcNow;
        Payment payment = Payment.Create(orderId, customerUserId: Guid.CreateVersion7(), amount: money, utcNow: now);

        if (targetStatus >= PaymentStatus.Authorized && intentId is not null)
        {
            payment.Authorize(intentId, now);
        }
        if (targetStatus == PaymentStatus.Captured || targetStatus == PaymentStatus.Refunded)
        {
            payment.Capture(now);
        }
        if (targetStatus == PaymentStatus.Voided)
        {
            payment.Void("seed", now);
        }
        if (targetStatus == PaymentStatus.Failed)
        {
            payment.Fail("seed", now);
        }
        if (targetStatus == PaymentStatus.NeedsReview)
        {
            payment.MarkNeedsReview("seed", now);
        }
        if (targetStatus == PaymentStatus.Refunded && refundId is not null)
        {
            payment.MarkRefunded(refundId, now);
        }

        db.Payments.Add(payment);
        await db.SaveChangesAsync();
    }

    // Helpers de espera inline por tipo: la conversion lambda->FilterDelegate del SDK de MassTransit
    // no funciona via un Func<IReceivedMessage<T>, bool> almacenado; debe formarse en el call site.

    private async Task WaitForOrderCancelledConsumedAsync(Guid orderId, int expected = 1)
    {
        IConsumerTestHarness<OrderTerminalCompensationConsumer> consumer =
            _provider.GetRequiredService<IConsumerTestHarness<OrderTerminalCompensationConsumer>>();
        for (int attempt = 0; attempt < 60; attempt++)
        {
            int consumed = consumer.Consumed
                .Select<OrderCancelledIntegrationEvent>(received => received.Context.Message.OrderId == orderId)
                .Count();
            if (consumed >= expected)
            {
                return;
            }

            await Task.Delay(100);
        }
    }

    private async Task WaitForOrderFailedConsumedAsync(Guid orderId, int expected = 1)
    {
        IConsumerTestHarness<OrderTerminalCompensationConsumer> consumer =
            _provider.GetRequiredService<IConsumerTestHarness<OrderTerminalCompensationConsumer>>();
        for (int attempt = 0; attempt < 60; attempt++)
        {
            int consumed = consumer.Consumed
                .Select<OrderFailedIntegrationEvent>(received => received.Context.Message.OrderId == orderId)
                .Count();
            if (consumed >= expected)
            {
                return;
            }

            await Task.Delay(100);
        }
    }

    private async Task WaitForRefundRequestedConsumedAsync(Guid orderId, int expected = 1)
    {
        IConsumerTestHarness<OrderTerminalCompensationConsumer> consumer =
            _provider.GetRequiredService<IConsumerTestHarness<OrderTerminalCompensationConsumer>>();
        for (int attempt = 0; attempt < 60; attempt++)
        {
            int consumed = consumer.Consumed
                .Select<RefundRequestedIntegrationEvent>(received => received.Context.Message.OrderId == orderId)
                .Count();
            if (consumed >= expected)
            {
                return;
            }

            await Task.Delay(100);
        }
    }
}
