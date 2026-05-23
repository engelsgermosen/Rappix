using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Rappix.Contracts.Dispatch;
using Rappix.Contracts.Orders;
using Rappix.Contracts.Payments;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Application.Configuration;
using Rappix.Orders.Application.Sagas;
using Rappix.Orders.Application.Sagas.Messages;
using Rappix.Orders.Domain.Common;
using Rappix.Orders.Domain.Orders;
using Rappix.Orders.Infrastructure.Messaging.Activities;
using Rappix.Orders.Infrastructure.Messaging.Simulation;

// Fase 6: el SimulatedCourierResponder se borro porque Dispatch lo reemplaza. En estos tests in-memory
// (sin Dispatch real ni broker), publicamos CourierAssignedIntegrationEvent / CourierUnavailableIntegrationEvent
// directamente en el harness para ejercer las transiciones de la saga.

namespace Rappix.Orders.Tests.Sagas;

/// <summary>
/// Pruebas de la saga (OrderStateMachine) con el Test Harness de MassTransit: repositorio de saga in-memory,
/// scheduler in-memory para los timeouts y clientes gRPC + repositorio mockeados (NSubstitute). Cubren el
/// camino feliz, cada rama de fallo con su compensacion, un timeout, la cancelacion y la idempotencia.
/// Las compensaciones se asertan en tres capas: comando publicado + cliente gRPC invocado + estado terminal.
/// </summary>
public sealed class OrderSagaTests
{
    [Fact]
    public async Task HappyPath_ReachesCompleted()
    {
        await using SagaContext context = await StartAsync();
        Guid orderId = await context.SubmitAsync();

        (await context.Saga.Exists(orderId, machine => machine.AwaitingMerchant)).Should().NotBeNull();

        await context.Harness.Bus.Publish(new MerchantAccepted(orderId));
        // Sin SimulatedCourierResponder: simulamos a Dispatch publicando CourierAssigned manualmente.
        (await context.Saga.Exists(orderId, machine => machine.AwaitingCourier)).Should().NotBeNull();
        await context.Harness.Bus.Publish(new CourierAssignedIntegrationEvent { OrderId = orderId, CourierId = Guid.CreateVersion7() });

        (await context.Saga.Exists(orderId, machine => machine.Completed)).Should().NotBeNull();
        (await context.Harness.Published.Any<OrderCompletedIntegrationEvent>()).Should().BeTrue();
    }

    [Fact]
    public async Task QuoteConsumptionFails_OrderFailed_NoCompensation()
    {
        await using SagaContext context = await StartAsync();
        context.Pricing.ConsumeQuoteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new PricingOperationResult(ServiceAvailable: true, Success: false, "Pricing.Quote.Expired", "Expired"));

        Guid orderId = await context.SubmitAsync();

        (await context.Saga.Exists(orderId, machine => machine.Failed)).Should().NotBeNull();
        (await context.Harness.Published.Any<OrderFailedIntegrationEvent>()).Should().BeTrue();
        await context.Stock.DidNotReceive().ReserveAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<StockReservationLineInput>>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StockReservationFails_RevertsQuote_Failed()
    {
        await using SagaContext context = await StartAsync();
        context.Stock.ReserveAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<StockReservationLineInput>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new StockOperationResult(ServiceAvailable: true, Success: false, "Catalog.Stock.Insufficient"));

        Guid orderId = await context.SubmitAsync();

        (await context.Saga.Exists(orderId, machine => machine.Failed)).Should().NotBeNull();
        (await context.Harness.Published.Any<RevertQuote>()).Should().BeTrue();
        await context.Pricing.Received().RevertQuoteAsync(Arg.Any<Guid>(), orderId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MerchantRejects_ReleasesStockAndRevertsQuote_Cancelled()
    {
        await using SagaContext context = await StartAsync();
        Guid orderId = await context.SubmitAsync();
        (await context.Saga.Exists(orderId, machine => machine.AwaitingMerchant)).Should().NotBeNull();

        await context.Harness.Bus.Publish(new MerchantRejected(orderId, "no hay ingredientes"));

        (await context.Saga.Exists(orderId, machine => machine.Cancelled)).Should().NotBeNull();
        (await context.Harness.Published.Any<ReleaseStock>()).Should().BeTrue();
        (await context.Harness.Published.Any<RevertQuote>()).Should().BeTrue();
        await context.Stock.Received().ReleaseAsync(orderId, Arg.Any<CancellationToken>());
        await context.Pricing.Received().RevertQuoteAsync(Arg.Any<Guid>(), orderId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PaymentFails_ReleasesStockAndRevertsQuote_Failed()
    {
        await using SagaContext context = await StartAsync(options => options.Simulation.PaymentOutcome = "Fail");
        Guid orderId = await context.SubmitAsync();
        (await context.Saga.Exists(orderId, machine => machine.AwaitingMerchant)).Should().NotBeNull();

        await context.Harness.Bus.Publish(new MerchantAccepted(orderId));

        (await context.Saga.Exists(orderId, machine => machine.Failed)).Should().NotBeNull();
        await context.Stock.Received().ReleaseAsync(orderId, Arg.Any<CancellationToken>());
        await context.Pricing.Received().RevertQuoteAsync(Arg.Any<Guid>(), orderId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CourierUnavailable_RefundsReleasesAndReverts_Cancelled()
    {
        await using SagaContext context = await StartAsync();
        Guid orderId = await context.SubmitAsync();
        (await context.Saga.Exists(orderId, machine => machine.AwaitingMerchant)).Should().NotBeNull();

        await context.Harness.Bus.Publish(new MerchantAccepted(orderId));
        // Sin SimulatedCourierResponder: simulamos a Dispatch publicando CourierUnavailable.
        (await context.Saga.Exists(orderId, machine => machine.AwaitingCourier)).Should().NotBeNull();
        await context.Harness.Bus.Publish(new CourierUnavailableIntegrationEvent { OrderId = orderId, Reason = "Sin couriers disponibles" });

        (await context.Saga.Exists(orderId, machine => machine.Cancelled)).Should().NotBeNull();
        (await context.Harness.Published.Any<RefundRequestedIntegrationEvent>()).Should().BeTrue();
        await context.Stock.Received().ReleaseAsync(orderId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MerchantTimeout_AutoCancels()
    {
        await using SagaContext context = await StartAsync(options => options.Timeouts.Merchant = TimeSpan.FromSeconds(1));
        Guid orderId = await context.SubmitAsync();

        // No se acepta: el timeout de merchant (1s) dispara la cancelacion + compensacion.
        (await context.Saga.Exists(orderId, machine => machine.Cancelled)).Should().NotBeNull();
        await context.Stock.Received().ReleaseAsync(orderId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelWhileAwaitingMerchant_Compensates_Cancelled()
    {
        await using SagaContext context = await StartAsync();
        Guid orderId = await context.SubmitAsync();
        (await context.Saga.Exists(orderId, machine => machine.AwaitingMerchant)).Should().NotBeNull();

        await context.Harness.Bus.Publish(new OrderCancellationRequested(orderId, "cliente cancelo"));

        (await context.Saga.Exists(orderId, machine => machine.Cancelled)).Should().NotBeNull();
        await context.Stock.Received().ReleaseAsync(orderId, Arg.Any<CancellationToken>());
        await context.Pricing.Received().RevertQuoteAsync(Arg.Any<Guid>(), orderId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DuplicateOrderSubmitted_CreatesSingleInstance()
    {
        await using SagaContext context = await StartAsync();
        Guid orderId = Guid.CreateVersion7();

        await context.SubmitAsync(orderId);
        (await context.Saga.Exists(orderId, machine => machine.AwaitingMerchant)).Should().NotBeNull();
        await context.SubmitAsync(orderId); // re-entrega

        // Sigue habiendo exactamente una instancia de saga para ese pedido.
        context.Saga.Sagas.Select(saga => saga.CorrelationId == orderId).Count().Should().Be(1);
    }

    [Fact]
    public async Task PaymentTimeout_AutoFails()
    {
        await using SagaContext context = await StartAsync(options =>
        {
            options.Timeouts.Payment = TimeSpan.FromSeconds(1);
            options.Simulation.PaymentOutcome = "Timeout"; // el responder no contesta
        });
        Guid orderId = await context.SubmitAsync();
        (await context.Saga.Exists(orderId, machine => machine.AwaitingMerchant)).Should().NotBeNull();

        await context.Harness.Bus.Publish(new MerchantAccepted(orderId));

        (await context.Saga.Exists(orderId, machine => machine.Failed)).Should().NotBeNull();
        await context.Stock.Received().ReleaseAsync(orderId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CourierTimeout_RefundsAndCancels()
    {
        // Sin SimulatedCourierResponder, ningun consumer responde CourierRequested. El timeout (1s) hace
        // el resto: compensa (refund + release + revert) y cancela. Esto valida que la saga sigue
        // protegida cuando Dispatch no responde a tiempo.
        await using SagaContext context = await StartAsync(options => options.Timeouts.Courier = TimeSpan.FromSeconds(1));
        Guid orderId = await context.SubmitAsync();
        (await context.Saga.Exists(orderId, machine => machine.AwaitingMerchant)).Should().NotBeNull();

        await context.Harness.Bus.Publish(new MerchantAccepted(orderId));

        (await context.Saga.Exists(orderId, machine => machine.Cancelled)).Should().NotBeNull();
        (await context.Harness.Published.Any<RefundRequestedIntegrationEvent>()).Should().BeTrue();
        await context.Stock.Received().ReleaseAsync(orderId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StockCommitFails_NeedsReview_NeverAutoRefunds()
    {
        await using SagaContext context = await StartAsync();
        context.Stock.CommitAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new StockOperationResult(ServiceAvailable: true, Success: false, "Catalog.Stock.CommitFailed"));

        Guid orderId = await context.SubmitAsync();
        (await context.Saga.Exists(orderId, machine => machine.AwaitingMerchant)).Should().NotBeNull();
        await context.Harness.Bus.Publish(new MerchantAccepted(orderId));
        // Sin SimulatedCourierResponder: publicamos CourierAssigned para avanzar a Committing.
        (await context.Saga.Exists(orderId, machine => machine.AwaitingCourier)).Should().NotBeNull();
        await context.Harness.Bus.Publish(new CourierAssignedIntegrationEvent { OrderId = orderId, CourierId = Guid.CreateVersion7() });

        // Pago OK + courier asignado -> CommitStock falla -> NeedsReview, conservando el dinero.
        (await context.Saga.Exists(orderId, machine => machine.NeedsReview)).Should().NotBeNull();
        await context.Stock.DidNotReceive().ReleaseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await context.Pricing.DidNotReceive().RevertQuoteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- Infraestructura de prueba ---

    private static async Task<SagaContext> StartAsync(Action<OrdersOptions>? configureOptions = null)
    {
        IPricingClient pricing = Substitute.For<IPricingClient>();
        IStockReservationClient stock = Substitute.For<IStockReservationClient>();
        IOrderRepository orders = Substitute.For<IOrderRepository>();

        // Defaults del camino feliz.
        pricing.ConsumeQuoteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new PricingOperationResult(true, true, string.Empty, "Consumed"));
        pricing.RevertQuoteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PricingOperationResult(true, true, string.Empty, "Active"));
        stock.ReserveAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<StockReservationLineInput>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new StockOperationResult(true, true, string.Empty));
        stock.CommitAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new StockOperationResult(true, true, string.Empty));
        stock.ReleaseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new StockOperationResult(true, true, string.Empty));
        orders.GetByIdAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns(FakeOrder());

        var options = new OrdersOptions
        {
            ReservationTtlSeconds = 1800,
            Timeouts = { Merchant = TimeSpan.FromSeconds(300), Payment = TimeSpan.FromSeconds(300), Courier = TimeSpan.FromSeconds(300) },
            Simulation = { PaymentOutcome = "Success", AutoDeliver = true, DeliveryDelayMs = 0 },
        };
        configureOptions?.Invoke(options);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IOptions<OrdersOptions>>(Options.Create(options));
        services.AddSingleton(pricing);
        services.AddSingleton(stock);
        services.AddSingleton(orders);
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.AddSagaStateMachine<OrderStateMachine, OrderState>().InMemoryRepository();
            configurator.AddConsumer<ConsumeQuoteConsumer>();
            configurator.AddConsumer<ReserveStockConsumer>();
            configurator.AddConsumer<CommitStockConsumer>();
            configurator.AddConsumer<ReleaseStockConsumer>();
            configurator.AddConsumer<RevertQuoteConsumer>();
            configurator.AddConsumer<SimulatedPaymentResponder>();
            // SimulatedCourierResponder eliminado en Fase 6: cada test publica CourierAssigned/Unavailable
            // manualmente para ejercer las transiciones (o deja correr el timeout).
            configurator.AddConsumer<SimulatedDeliveryResponder>();
        });

        ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        ITestHarness harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        return new SagaContext(provider, harness, pricing, stock, orders);
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

    private sealed class SagaContext(
        ServiceProvider provider,
        ITestHarness harness,
        IPricingClient pricing,
        IStockReservationClient stock,
        IOrderRepository orders) : IAsyncDisposable
    {
        public ITestHarness Harness => harness;
        public IPricingClient Pricing => pricing;
        public IStockReservationClient Stock => stock;
        public IOrderRepository Orders => orders;
        public ISagaStateMachineTestHarness<OrderStateMachine, OrderState> Saga =>
            harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();

        public async Task<Guid> SubmitAsync(Guid? orderId = null)
        {
            Guid id = orderId ?? Guid.CreateVersion7();
            await harness.Bus.Publish(new OrderSubmittedIntegrationEvent
            {
                OrderId = id,
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
            return id;
        }

        public async ValueTask DisposeAsync()
        {
            await harness.Stop();
            await provider.DisposeAsync();
        }
    }
}
