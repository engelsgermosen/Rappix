using MassTransit;
using Microsoft.Extensions.Options;
using Rappix.Contracts.Dispatch;
using Rappix.Contracts.Orders;
using Rappix.Contracts.Payments;
using Rappix.Orders.Application.Configuration;
using Rappix.Orders.Application.Sagas.Messages;
using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Application.Sagas;

/// <summary>
/// Saga orquestada del pedido (corazon de la fase). Reacciona SOLO a mensajes: al entrar a un estado publica
/// un comando (que un consumer "activity" ejecuta via gRPC) o un evento de integracion, y avanza al recibir
/// la respuesta. Los timeouts se programan con el scheduler (Quartz) y se cancelan al recibir el evento
/// esperado. Las compensaciones se hacen en orden inverso (release stock -> revert quote) con confirmacion.
///
/// Flujo feliz:
/// OrderSubmitted -> ConsumeQuote -> ReserveStock -> (notifica) AwaitingMerchant -> MerchantAccepted ->
/// PaymentRequested -> PaymentSucceeded -> CourierRequested -> CourierAssigned -> CommitStock ->
/// InProgress -> OrderDelivered -> Completed.
/// </summary>
public sealed class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    private readonly int _reservationTtlSeconds;

    public OrderStateMachine(IOptions<OrdersOptions> options)
    {
        OrdersOptions config = options.Value;
        _reservationTtlSeconds = config.ReservationTtlSeconds;

        InstanceState(state => state.CurrentState);

        ConfigureCorrelation();

        Schedule(() => MerchantTimeout, state => state.MerchantTimeoutTokenId, schedule =>
        {
            schedule.Delay = config.Timeouts.Merchant;
            schedule.Received = received => received.CorrelateById(context => context.Message.OrderId);
        });
        Schedule(() => PaymentTimeout, state => state.PaymentTimeoutTokenId, schedule =>
        {
            schedule.Delay = config.Timeouts.Payment;
            schedule.Received = received => received.CorrelateById(context => context.Message.OrderId);
        });
        Schedule(() => CourierTimeout, state => state.CourierTimeoutTokenId, schedule =>
        {
            schedule.Delay = config.Timeouts.Courier;
            schedule.Received = received => received.CorrelateById(context => context.Message.OrderId);
        });

        // Eventos stale/duplicados que no encajan en el estado actual se ignoran (entrega at-least-once).
        // Las carreras importantes (reserva/pago tardios en compensacion) se manejan explicitamente abajo.
        OnUnhandledEvent(context => context.Ignore());

        Initially(
            When(OrderSubmitted)
                .Then(context =>
                {
                    context.Saga.QuoteId = context.Message.QuoteId;
                    context.Saga.CustomerUserId = context.Message.CustomerUserId;
                    context.Saga.MerchantId = context.Message.MerchantId;
                    context.Saga.TotalAmount = context.Message.TotalAmount;
                    context.Saga.Currency = context.Message.Currency;
                    context.Saga.DeliveryLatitude = context.Message.DeliveryLatitude;
                    context.Saga.DeliveryLongitude = context.Message.DeliveryLongitude;
                    context.Saga.PickupLatitude = context.Message.PickupLatitude;
                    context.Saga.PickupLongitude = context.Message.PickupLongitude;
                })
                .Publish(context => new ConsumeQuote(context.Saga.CorrelationId, context.Saga.QuoteId))
                .TransitionTo(ValidatingQuote));

        During(ValidatingQuote,
            When(QuoteConsumed)
                .Publish(context => new ReserveStock(context.Saga.CorrelationId, _reservationTtlSeconds))
                .TransitionTo(ReservingStock),
            When(QuoteConsumptionFailed)
                .Then(context => context.Saga.Reason = context.Message.Reason)
                .Then(context => context.Saga.CompensationTerminal = OrderStatus.Failed)
                // Nada adquirido aun: a Failed directo, sin compensar.
                .Publish(context => new OrderStatusChanged(context.Saga.CorrelationId, OrderStatus.Failed, DateTime.UtcNow, context.Saga.Reason))
                .Publish(context => new OrderFailedIntegrationEvent { OrderId = context.Saga.CorrelationId, Reason = context.Saga.Reason ?? "No se pudo consumir la cotizacion", FailedAtUtc = DateTime.UtcNow })
                .TransitionTo(Failed),
            // Cancelacion mientras se consume: revertir (no-op si aun no se consumio); el quote consumido tardio se maneja en CompensatingQuote.
            CompensateQuoteOnly(When(CancellationRequested).Then(context => context.Saga.Reason = context.Message.Reason), OrderStatus.Cancelled));

        During(ReservingStock,
            When(StockReserved)
                .Schedule(MerchantTimeout, context => new MerchantTimeoutExpired(context.Saga.CorrelationId))
                .Publish(context => new OrderStatusChanged(context.Saga.CorrelationId, OrderStatus.AwaitingMerchant, DateTime.UtcNow, null))
                .TransitionTo(AwaitingMerchant),
            // Quote consumida pero sin stock: compensar solo la quote.
            CompensateQuoteOnly(When(StockReservationFailed).Then(context => context.Saga.Reason = context.Message.Reason), OrderStatus.Failed),
            // Cancelacion: pasa por release (cubre una reserva que llegue tarde) y luego revert.
            StartCompensation(When(CancellationRequested).Then(context => context.Saga.Reason = context.Message.Reason), OrderStatus.Cancelled));

        During(AwaitingMerchant,
            When(MerchantAccepted)
                .Unschedule(MerchantTimeout)
                .Publish(context => new OrderAcceptedIntegrationEvent { OrderId = context.Saga.CorrelationId, MerchantId = context.Saga.MerchantId, AcceptedAtUtc = DateTime.UtcNow })
                .Publish(context => new PaymentRequestedIntegrationEvent { OrderId = context.Saga.CorrelationId, CustomerUserId = context.Saga.CustomerUserId, Amount = context.Saga.TotalAmount, Currency = context.Saga.Currency })
                .Schedule(PaymentTimeout, context => new PaymentTimeoutExpired(context.Saga.CorrelationId))
                .Publish(context => new OrderStatusChanged(context.Saga.CorrelationId, OrderStatus.AwaitingPayment, DateTime.UtcNow, null))
                .TransitionTo(AwaitingPayment),
            StartCompensation(When(MerchantRejected).Then(context => context.Saga.Reason = context.Message.Reason).Unschedule(MerchantTimeout), OrderStatus.Cancelled),
            StartCompensation(When(MerchantTimeout.Received).Then(context => context.Saga.Reason = "El merchant no acepto a tiempo"), OrderStatus.Cancelled),
            StartCompensation(When(CancellationRequested).Then(context => context.Saga.Reason = context.Message.Reason).Unschedule(MerchantTimeout), OrderStatus.Cancelled));

        During(AwaitingPayment,
            When(PaymentSucceeded)
                .Unschedule(PaymentTimeout)
                .Then(context =>
                {
                    context.Saga.PaymentCaptured = true;
                    context.Saga.PaymentId = context.Message.PaymentId;
                })
                .Publish(context => new CourierRequestedIntegrationEvent { OrderId = context.Saga.CorrelationId, MerchantId = context.Saga.MerchantId, PickupLatitude = context.Saga.PickupLatitude, PickupLongitude = context.Saga.PickupLongitude, DeliveryLatitude = context.Saga.DeliveryLatitude, DeliveryLongitude = context.Saga.DeliveryLongitude })
                .Schedule(CourierTimeout, context => new CourierTimeoutExpired(context.Saga.CorrelationId))
                .Publish(context => new OrderStatusChanged(context.Saga.CorrelationId, OrderStatus.AwaitingCourier, DateTime.UtcNow, null))
                .TransitionTo(AwaitingCourier),
            StartCompensation(When(PaymentFailed).Then(context => context.Saga.Reason = context.Message.Reason).Unschedule(PaymentTimeout), OrderStatus.Failed),
            StartCompensation(When(PaymentTimeout.Received).Then(context => context.Saga.Reason = "El pago no se confirmo a tiempo"), OrderStatus.Failed),
            StartCompensation(When(CancellationRequested).Then(context => context.Saga.Reason = context.Message.Reason).Unschedule(PaymentTimeout), OrderStatus.Cancelled));

        During(AwaitingCourier,
            When(CourierAssigned)
                .Unschedule(CourierTimeout)
                .Then(context => context.Saga.CourierId = context.Message.CourierId)
                .Publish(context => new CommitStock(context.Saga.CorrelationId))
                .TransitionTo(Committing),
            StartCompensation(When(CourierUnavailable).Then(context => context.Saga.Reason = context.Message.Reason).Unschedule(CourierTimeout), OrderStatus.Cancelled),
            StartCompensation(When(CourierTimeout.Received).Then(context => context.Saga.Reason = "No se asigno courier a tiempo"), OrderStatus.Cancelled),
            StartCompensation(When(CancellationRequested).Then(context => context.Saga.Reason = context.Message.Reason).Unschedule(CourierTimeout), OrderStatus.Cancelled));

        During(Committing,
            When(StockCommitted)
                .Publish(context => new OrderStatusChanged(context.Saga.CorrelationId, OrderStatus.InProgress, DateTime.UtcNow, null))
                .TransitionTo(InProgress),
            // El commit no deberia fallar (la reserva ya garantiza el inventario): NO se auto-reembolsa.
            // Tras los reintentos del bus, se marca para revision manual conservando el dinero.
            When(StockCommitFailed)
                .Then(context => context.Saga.Reason = context.Message.Reason)
                .Publish(context => new OrderStatusChanged(context.Saga.CorrelationId, OrderStatus.NeedsReview, DateTime.UtcNow, context.Saga.Reason))
                .TransitionTo(NeedsReview));

        During(InProgress,
            When(OrderDelivered)
                .Publish(context => new OrderStatusChanged(context.Saga.CorrelationId, OrderStatus.Completed, DateTime.UtcNow, null))
                .Publish(context => new OrderCompletedIntegrationEvent { OrderId = context.Saga.CorrelationId, CustomerUserId = context.Saga.CustomerUserId, MerchantId = context.Saga.MerchantId, CompletedAtUtc = DateTime.UtcNow })
                .TransitionTo(Completed));

        During(CompensatingStock,
            When(StockReleased)
                .Publish(context => new RevertQuote(context.Saga.CorrelationId, context.Saga.QuoteId, context.Saga.Reason ?? "Compensacion"))
                .TransitionTo(CompensatingQuote),
            // Carrera: una reserva que llega tarde (despues de empezar a compensar) se vuelve a liberar.
            When(StockReserved).Publish(context => new ReleaseStock(context.Saga.CorrelationId)),
            // Carrera: un pago que llega tarde se reembolsa.
            When(PaymentSucceeded).Then(context => context.Saga.PaymentCaptured = true).Publish(context => new RefundRequestedIntegrationEvent { OrderId = context.Saga.CorrelationId, Amount = context.Saga.TotalAmount, Currency = context.Saga.Currency, Reason = "Pago recibido tras cancelar" }));

        During(CompensatingQuote,
            FinalizeCompensation(When(QuoteReverted)),
            // La reversion fallo: se finaliza igual (best-effort); la inconsistencia se audita en Pricing.
            FinalizeCompensation(When(QuoteRevertFailed).Then(context => context.Saga.Reason = context.Saga.Reason ?? context.Message.Reason)),
            // Carrera: una quote consumida tardiamente se vuelve a revertir.
            When(QuoteConsumed).Publish(context => new RevertQuote(context.Saga.CorrelationId, context.Saga.QuoteId, context.Saga.Reason ?? "Compensacion")),
            When(PaymentSucceeded).Then(context => context.Saga.PaymentCaptured = true).Publish(context => new RefundRequestedIntegrationEvent { OrderId = context.Saga.CorrelationId, Amount = context.Saga.TotalAmount, Currency = context.Saga.Currency, Reason = "Pago recibido tras cancelar" }));
    }

    // --- Estados ---

    /// <summary>Consumiendo la cotizacion (congelar precio, redimir cupon).</summary>
    public State ValidatingQuote { get; private set; } = null!;

    /// <summary>Reservando (hold) el stock.</summary>
    public State ReservingStock { get; private set; } = null!;

    /// <summary>Esperando aceptacion del merchant (con timeout).</summary>
    public State AwaitingMerchant { get; private set; } = null!;

    /// <summary>Esperando confirmacion del pago (con timeout).</summary>
    public State AwaitingPayment { get; private set; } = null!;

    /// <summary>Esperando asignacion de courier (con timeout).</summary>
    public State AwaitingCourier { get; private set; } = null!;

    /// <summary>Confirmando el stock reservado (decremento definitivo).</summary>
    public State Committing { get; private set; } = null!;

    /// <summary>En curso (courier asignado, stock confirmado).</summary>
    public State InProgress { get; private set; } = null!;

    /// <summary>Compensando: liberando el stock reservado.</summary>
    public State CompensatingStock { get; private set; } = null!;

    /// <summary>Compensando: revirtiendo el consumo de la cotizacion.</summary>
    public State CompensatingQuote { get; private set; } = null!;

    /// <summary>Completado (terminal feliz).</summary>
    public State Completed { get; private set; } = null!;

    /// <summary>Cancelado (terminal).</summary>
    public State Cancelled { get; private set; } = null!;

    /// <summary>Fallido (terminal).</summary>
    public State Failed { get; private set; } = null!;

    /// <summary>Requiere revision manual (terminal): fallo de commit tras cobrar.</summary>
    public State NeedsReview { get; private set; } = null!;

    // --- Eventos ---

    /// <summary>Inicia la saga (cliente envio el pedido).</summary>
    public Event<OrderSubmittedIntegrationEvent> OrderSubmitted { get; private set; } = null!;

    public Event<QuoteConsumed> QuoteConsumed { get; private set; } = null!;
    public Event<QuoteConsumptionFailed> QuoteConsumptionFailed { get; private set; } = null!;
    public Event<StockReserved> StockReserved { get; private set; } = null!;
    public Event<StockReservationFailed> StockReservationFailed { get; private set; } = null!;
    public Event<MerchantAccepted> MerchantAccepted { get; private set; } = null!;
    public Event<MerchantRejected> MerchantRejected { get; private set; } = null!;
    public Event<PaymentSucceededIntegrationEvent> PaymentSucceeded { get; private set; } = null!;
    public Event<PaymentFailedIntegrationEvent> PaymentFailed { get; private set; } = null!;
    public Event<CourierAssignedIntegrationEvent> CourierAssigned { get; private set; } = null!;
    public Event<CourierUnavailableIntegrationEvent> CourierUnavailable { get; private set; } = null!;
    public Event<StockCommitted> StockCommitted { get; private set; } = null!;
    public Event<StockCommitFailed> StockCommitFailed { get; private set; } = null!;
    public Event<OrderDeliveredIntegrationEvent> OrderDelivered { get; private set; } = null!;
    public Event<StockReleased> StockReleased { get; private set; } = null!;
    public Event<QuoteReverted> QuoteReverted { get; private set; } = null!;
    public Event<QuoteRevertFailed> QuoteRevertFailed { get; private set; } = null!;
    public Event<OrderCancellationRequested> CancellationRequested { get; private set; } = null!;

    // --- Timeouts programados ---

    public Schedule<OrderState, MerchantTimeoutExpired> MerchantTimeout { get; private set; } = null!;
    public Schedule<OrderState, PaymentTimeoutExpired> PaymentTimeout { get; private set; } = null!;
    public Schedule<OrderState, CourierTimeoutExpired> CourierTimeout { get; private set; } = null!;

    private void ConfigureCorrelation()
    {
        Event(() => OrderSubmitted, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => QuoteConsumed, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => QuoteConsumptionFailed, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => StockReserved, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => StockReservationFailed, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => MerchantAccepted, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => MerchantRejected, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => PaymentSucceeded, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => PaymentFailed, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => CourierAssigned, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => CourierUnavailable, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => StockCommitted, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => StockCommitFailed, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => OrderDelivered, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => StockReleased, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => QuoteReverted, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => QuoteRevertFailed, e => e.CorrelateById(context => context.Message.OrderId));
        Event(() => CancellationRequested, e => e.CorrelateById(context => context.Message.OrderId));
    }

    /// <summary>Inicia la compensacion completa (orden inverso): reembolsa si se cobro, libera stock, luego revierte quote.</summary>
    private EventActivityBinder<OrderState, TEvent> StartCompensation<TEvent>(EventActivityBinder<OrderState, TEvent> binder, OrderStatus terminal)
        where TEvent : class =>
        binder
            .Then(context => context.Saga.CompensationTerminal = terminal)
            .IfElse(context => context.Saga.PaymentCaptured,
                refund => refund.Publish(context => new RefundRequestedIntegrationEvent { OrderId = context.Saga.CorrelationId, Amount = context.Saga.TotalAmount, Currency = context.Saga.Currency, Reason = context.Saga.Reason ?? "Compensacion del pedido" }),
                noRefund => noRefund)
            .Publish(context => new ReleaseStock(context.Saga.CorrelationId))
            .TransitionTo(CompensatingStock);

    /// <summary>Compensa solo la quote (cuando no hay stock reservado que liberar).</summary>
    private EventActivityBinder<OrderState, TEvent> CompensateQuoteOnly<TEvent>(EventActivityBinder<OrderState, TEvent> binder, OrderStatus terminal)
        where TEvent : class =>
        binder
            .Then(context => context.Saga.CompensationTerminal = terminal)
            .Publish(context => new RevertQuote(context.Saga.CorrelationId, context.Saga.QuoteId, context.Saga.Reason ?? "Compensacion"))
            .TransitionTo(CompensatingQuote);

    /// <summary>Finaliza la compensacion: publica el evento terminal correcto (Cancelled/Failed) y transiciona.</summary>
    private EventActivityBinder<OrderState, TEvent> FinalizeCompensation<TEvent>(EventActivityBinder<OrderState, TEvent> binder)
        where TEvent : class =>
        binder.IfElse(context => context.Saga.CompensationTerminal == OrderStatus.Failed,
            failed => failed
                .Publish(context => new OrderStatusChanged(context.Saga.CorrelationId, OrderStatus.Failed, DateTime.UtcNow, context.Saga.Reason))
                .Publish(context => new OrderFailedIntegrationEvent { OrderId = context.Saga.CorrelationId, Reason = context.Saga.Reason ?? "Pedido fallido", FailedAtUtc = DateTime.UtcNow })
                .TransitionTo(Failed),
            cancelled => cancelled
                .Publish(context => new OrderStatusChanged(context.Saga.CorrelationId, OrderStatus.Cancelled, DateTime.UtcNow, context.Saga.Reason))
                .Publish(context => new OrderCancelledIntegrationEvent { OrderId = context.Saga.CorrelationId, Reason = context.Saga.Reason ?? "Pedido cancelado", CancelledAtUtc = DateTime.UtcNow })
                .TransitionTo(Cancelled));
}
