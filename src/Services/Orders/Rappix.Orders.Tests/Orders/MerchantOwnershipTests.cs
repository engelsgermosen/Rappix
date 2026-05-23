using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Application.Orders.Accept;
using Rappix.Orders.Application.Orders.Reject;
using Rappix.Orders.Application.Sagas.Messages;
using Rappix.Orders.Domain.Common;
using Rappix.Orders.Domain.Errors;
using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Tests.Orders;

/// <summary>
/// Verifica la autorizacion del merchant por OwnerUserId (no por MerchantId): el dueno (sub del JWT ==
/// Order.MerchantOwnerUserId) puede aceptar/rechazar y se publica el mensaje de saga; otro merchant recibe
/// NotForMerchant (Forbidden -> 403). Cierra el caveat "merchant identity simplified to JWT sub".
/// </summary>
public sealed class MerchantOwnershipTests
{
    [Fact]
    public async Task AcceptOrder_ByOwner_SucceedsAndPublishesMerchantAccepted()
    {
        Guid ownerUserId = Guid.CreateVersion7();
        Order order = OrderOwnedBy(ownerUserId);
        IOrderRepository orders = OrdersReturning(order);

        await using ServiceProvider provider = new ServiceCollection().AddMassTransitTestHarness().BuildServiceProvider(validateScopes: true);
        ITestHarness harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        var handler = new AcceptOrderCommandHandler(orders, harness.Bus, unitOfWork);
        Result result = await handler.Handle(new AcceptOrderCommand(order.Id.Value, ownerUserId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await harness.Published.Any<MerchantAccepted>()).Should().BeTrue();
        // Imprescindible con UseBusOutbox: el publish solo se entrega al hacer SaveChanges (vacia el buffer).
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        await harness.Stop();
    }

    [Fact]
    public async Task AcceptOrder_ByNonOwner_ReturnsNotForMerchant()
    {
        Order order = OrderOwnedBy(Guid.CreateVersion7());
        IOrderRepository orders = OrdersReturning(order);

        var handler = new AcceptOrderCommandHandler(orders, Substitute.For<IPublishEndpoint>(), Substitute.For<IUnitOfWork>());
        Result result = await handler.Handle(new AcceptOrderCommand(order.Id.Value, Guid.CreateVersion7()), CancellationToken.None);

        result.Error.Should().Be(OrderErrors.NotForMerchant);
    }

    [Fact]
    public async Task RejectOrder_ByOwner_SucceedsAndPublishesMerchantRejected()
    {
        Guid ownerUserId = Guid.CreateVersion7();
        Order order = OrderOwnedBy(ownerUserId);
        IOrderRepository orders = OrdersReturning(order);

        await using ServiceProvider provider = new ServiceCollection().AddMassTransitTestHarness().BuildServiceProvider(validateScopes: true);
        ITestHarness harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        var handler = new RejectOrderCommandHandler(orders, harness.Bus, unitOfWork);
        Result result = await handler.Handle(new RejectOrderCommand(order.Id.Value, ownerUserId, "sin stock"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await harness.Published.Any<MerchantRejected>()).Should().BeTrue();
        // Imprescindible con UseBusOutbox: el publish solo se entrega al hacer SaveChanges (vacia el buffer).
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        await harness.Stop();
    }

    [Fact]
    public async Task RejectOrder_ByNonOwner_ReturnsNotForMerchant()
    {
        Order order = OrderOwnedBy(Guid.CreateVersion7());
        IOrderRepository orders = OrdersReturning(order);

        var handler = new RejectOrderCommandHandler(orders, Substitute.For<IPublishEndpoint>(), Substitute.For<IUnitOfWork>());
        Result result = await handler.Handle(new RejectOrderCommand(order.Id.Value, Guid.CreateVersion7(), "x"), CancellationToken.None);

        result.Error.Should().Be(OrderErrors.NotForMerchant);
    }

    private static IOrderRepository OrdersReturning(Order order)
    {
        IOrderRepository orders = Substitute.For<IOrderRepository>();
        orders.GetByIdAsync(Arg.Any<OrderId>(), Arg.Any<CancellationToken>()).Returns(order);
        return orders;
    }

    private static Order OrderOwnedBy(Guid ownerUserId)
    {
        OrderLine line = OrderLine.Create(Guid.CreateVersion7(), "Pizza", 100m, 0m, 1).Value;
        DeliveryAddress address = DeliveryAddress.Create("Calle 1", null, 18.48, -69.93).Value;
        return Order.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), ownerUserId, Guid.CreateVersion7(), "Food", "DOP", [line],
            100m, 50m, 0m, 0m, 0m, 0m, 150m, address,
            pickupLatitude: 18.4861, pickupLongitude: -69.9312,
            DateTime.UtcNow).Value;
    }
}
