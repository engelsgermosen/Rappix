using FluentAssertions;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Orders.Domain.Common;
using Rappix.Orders.Domain.Orders;
using Rappix.Orders.Domain.Orders.Events;

namespace Rappix.Orders.Tests.Unit;

/// <summary>Pruebas del agregado Order: creacion (snapshot + evento) y transiciones de estado de la proyeccion.</summary>
public sealed class OrderTests
{
    private static readonly DateTime Now = new(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_Succeeds_RaisesOrderSubmittedDomainEvent()
    {
        Order order = NewOrder();

        order.Status.Should().Be(OrderStatus.Submitted);
        order.TotalAmount.Should().Be(250m);
        order.Lines.Should().ContainSingle();
        order.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is OrderSubmittedDomainEvent);
    }

    [Fact]
    public void Create_NoLines_Fails()
    {
        Result<Order> result = Order.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "Food", "DOP", [],
            200m, 50m, 0m, 0m, 0m, 0m, 250m, Address(), Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ApplyStatus_AwaitingPayment_SetsAcceptedAt()
    {
        Order order = NewOrder();

        order.ApplyStatus(OrderStatus.AwaitingPayment, Now.AddMinutes(1));

        order.Status.Should().Be(OrderStatus.AwaitingPayment);
        order.AcceptedAtUtc.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void ApplyStatus_Cancelled_SetsReasonAndClosedAt()
    {
        Order order = NewOrder();

        order.ApplyStatus(OrderStatus.Cancelled, Now.AddMinutes(2), "merchant rechazo");

        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancellationReason.Should().Be("merchant rechazo");
        order.ClosedAtUtc.Should().Be(Now.AddMinutes(2));
        order.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void ApplyStatus_FromTerminal_IsIgnored()
    {
        Order order = NewOrder();
        order.ApplyStatus(OrderStatus.Completed, Now.AddMinutes(5));

        order.ApplyStatus(OrderStatus.Cancelled, Now.AddMinutes(6), "tarde");

        order.Status.Should().Be(OrderStatus.Completed);
        order.CancellationReason.Should().BeNull();
    }

    [Fact]
    public void IsCancellable_OnlyBeforeCommitting()
    {
        Order order = NewOrder();
        order.IsCancellable.Should().BeTrue();

        order.ApplyStatus(OrderStatus.AwaitingCourier, Now.AddMinutes(1));
        order.IsCancellable.Should().BeTrue();

        order.ApplyStatus(OrderStatus.InProgress, Now.AddMinutes(2));
        order.IsCancellable.Should().BeFalse();
    }

    private static DeliveryAddress Address() =>
        DeliveryAddress.Create("Calle 1", null, 18.48, -69.93).Value;

    private static Order NewOrder()
    {
        OrderLine line = OrderLine.Create(Guid.CreateVersion7(), "Pizza", 100m, 25m, 2).Value;
        return Order.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "Food", "DOP", [line],
            200m, 50m, 0m, 0m, 0m, 0m, 250m, Address(), Now).Value;
    }
}
