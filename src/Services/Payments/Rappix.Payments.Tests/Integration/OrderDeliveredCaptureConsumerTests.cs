using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Dispatch;
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
/// Pruebas del <see cref="OrderDeliveredCaptureConsumer"/>. Cada test arranca su propio container
/// PostgreSQL via <see cref="IAsyncLifetime"/> (mismo patron de aislamiento que el resto de la suite).
/// </summary>
public sealed class OrderDeliveredCaptureConsumerTests : IAsyncLifetime
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
        services.AddMassTransitTestHarness(configurator => configurator.AddConsumer<OrderDeliveredCaptureConsumer>());

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

    [Fact]
    public async Task HappyPath_CapturesAuthorizedPayment_CallsGatewayOnce()
    {
        Guid orderId = Guid.CreateVersion7();
        await SeedPaymentInStatusAsync(orderId, PaymentStatus.Authorized, intentId: "pi_fake_001");

        await Harness.Bus.Publish(new OrderDeliveredIntegrationEvent
        {
            OrderId = orderId,
            DeliveredAtUtc = DateTime.UtcNow,
        });
        await WaitForConsumedAsync(orderId);

        Payment? payment = await GetPaymentAsync(orderId);
        payment.Should().NotBeNull();
        payment!.Status.Should().Be(PaymentStatus.Captured);
        payment.ProviderPaymentIntentId.Should().Be("pi_fake_001"); // mismo intent del Authorize

        Counter.CaptureCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Idempotent_WhenAlreadyCaptured_DoesNotCallGateway_KeepsCapturedStatus()
    {
        // Re-entrega del OrderDelivered: el consumer ve Status=Captured y no-op.
        Guid orderId = Guid.CreateVersion7();
        await SeedPaymentInStatusAsync(orderId, PaymentStatus.Captured, intentId: "pi_fake_002");

        await Harness.Bus.Publish(new OrderDeliveredIntegrationEvent
        {
            OrderId = orderId,
            DeliveredAtUtc = DateTime.UtcNow,
        });
        await WaitForConsumedAsync(orderId);

        Payment? payment = await GetPaymentAsync(orderId);
        payment!.Status.Should().Be(PaymentStatus.Captured);
        Counter.CaptureCallCount.Should().Be(0);
    }

    [Fact]
    public async Task NoPayment_LogsAndNoOp_DoesNotCallGateway()
    {
        // OrderDelivered llega para un pedido sin Payment (caso raro pero defensivo).
        Guid orderId = Guid.CreateVersion7();

        await Harness.Bus.Publish(new OrderDeliveredIntegrationEvent
        {
            OrderId = orderId,
            DeliveredAtUtc = DateTime.UtcNow,
        });
        await WaitForConsumedAsync(orderId);

        (await CountPaymentsAsync(orderId)).Should().Be(0);
        Counter.CaptureCallCount.Should().Be(0);
    }

    [Fact]
    public async Task PendingPayment_LogsWarning_DoesNotCallGateway_KeepsPendingStatus()
    {
        // Caso defensivo: OrderDelivered antes de Authorize (no deberia ocurrir; saga no avanza sin
        // PaymentSucceeded). Si pasara, NO capturar — log + no-op.
        Guid orderId = Guid.CreateVersion7();
        await SeedPaymentInStatusAsync(orderId, PaymentStatus.Pending, intentId: null);

        await Harness.Bus.Publish(new OrderDeliveredIntegrationEvent
        {
            OrderId = orderId,
            DeliveredAtUtc = DateTime.UtcNow,
        });
        await WaitForConsumedAsync(orderId);

        Payment? payment = await GetPaymentAsync(orderId);
        payment!.Status.Should().Be(PaymentStatus.Pending);
        Counter.CaptureCallCount.Should().Be(0);
    }

    [Fact]
    public async Task VoidedPayment_LogsWarning_DoesNotCallGateway_KeepsVoidedStatus()
    {
        // Caso de carrera muy raro: el hold fue void (cancelado) pero llega OrderDelivered. El courier
        // entrego pero el pago se cancelo. Caso a auditar humanamente; Payments no toca el aggregate.
        Guid orderId = Guid.CreateVersion7();
        await SeedPaymentInStatusAsync(orderId, PaymentStatus.Voided, intentId: "pi_fake_003");

        await Harness.Bus.Publish(new OrderDeliveredIntegrationEvent
        {
            OrderId = orderId,
            DeliveredAtUtc = DateTime.UtcNow,
        });
        await WaitForConsumedAsync(orderId);

        Payment? payment = await GetPaymentAsync(orderId);
        payment!.Status.Should().Be(PaymentStatus.Voided);
        Counter.CaptureCallCount.Should().Be(0);
    }

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

    private async Task SeedPaymentInStatusAsync(Guid orderId, PaymentStatus targetStatus, string? intentId)
    {
        using IServiceScope scope = _provider.CreateScope();
        PaymentsDbContext db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        Money money = Money.Create(100m, "DOP").Value;
        DateTime now = DateTime.UtcNow;
        Payment payment = Payment.Create(orderId, customerUserId: Guid.CreateVersion7(), amount: money, utcNow: now);

        // Drive el aggregate al status pedido via los metodos del dominio (no via reflection).
        if (targetStatus >= PaymentStatus.Authorized && intentId is not null)
        {
            payment.Authorize(intentId, now);
        }
        if (targetStatus == PaymentStatus.Captured)
        {
            payment.Capture(now);
        }
        if (targetStatus == PaymentStatus.Voided)
        {
            payment.Void("test seed void", now);
        }
        if (targetStatus == PaymentStatus.Failed)
        {
            payment.Fail("test seed failed", now);
        }

        db.Payments.Add(payment);
        await db.SaveChangesAsync();
    }

    private async Task WaitForConsumedAsync(Guid orderId, int expected = 1)
    {
        IConsumerTestHarness<OrderDeliveredCaptureConsumer> consumer =
            _provider.GetRequiredService<IConsumerTestHarness<OrderDeliveredCaptureConsumer>>();
        for (int attempt = 0; attempt < 60; attempt++)
        {
            int consumed = consumer.Consumed
                .Select<OrderDeliveredIntegrationEvent>(received => received.Context.Message.OrderId == orderId)
                .Count();
            if (consumed >= expected)
            {
                return;
            }

            await Task.Delay(100);
        }
    }
}
