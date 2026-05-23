using FluentAssertions;
using NSubstitute;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Tracking.Application.Abstractions;
using Rappix.Tracking.Application.OrderTrackings;
using Rappix.Tracking.Application.OrderTrackings.GetOrderTracking;
using Rappix.Tracking.Application.OrderTrackings.Responses;
using Rappix.Tracking.Domain.OrderTrackings;

namespace Rappix.Tracking.Tests.Unit;

/// <summary>
/// Unit tests del query handler. Validan los dos casos criticos del read side: snapshot inexistente
/// y snapshot que NO pertenece al usuario — ambos colapsan al mismo <see cref="OrderTrackingErrors.NotFound"/>.
/// </summary>
public sealed class GetOrderTrackingQueryHandlerTests
{
    private readonly IOrderTrackingReadRepository _readRepository = Substitute.For<IOrderTrackingReadRepository>();

    [Fact]
    public async Task Handle_ReturnsSnapshot_WhenOwnerMatches()
    {
        Guid orderId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        OrderTrackingSnapshot snapshot = CreateSnapshot(orderId, customerUserId: userId);
        _readRepository.GetSnapshotAsync(orderId, Arg.Any<CancellationToken>()).Returns(snapshot);

        Result<OrderTrackingResponse> result = await new GetOrderTrackingQueryHandler(_readRepository)
            .Handle(new GetOrderTrackingQuery(orderId, userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.OrderId.Should().Be(orderId);
        result.Value.Status.Should().Be("Placed");
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenSnapshotDoesNotExist()
    {
        Guid orderId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        _readRepository.GetSnapshotAsync(orderId, Arg.Any<CancellationToken>()).Returns((OrderTrackingSnapshot?)null);

        Result<OrderTrackingResponse> result = await new GetOrderTrackingQueryHandler(_readRepository)
            .Handle(new GetOrderTrackingQuery(orderId, userId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(OrderTrackingErrors.NotFound);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenSnapshotExistsButOwnerDoesNotMatch()
    {
        // Cliente A intenta consultar el pedido de cliente B: NotFound (no se revela que existe).
        Guid orderId = Guid.NewGuid();
        Guid otherUser = Guid.NewGuid();
        Guid attacker = Guid.NewGuid();
        OrderTrackingSnapshot snapshot = CreateSnapshot(orderId, customerUserId: otherUser);
        _readRepository.GetSnapshotAsync(orderId, Arg.Any<CancellationToken>()).Returns(snapshot);

        Result<OrderTrackingResponse> result = await new GetOrderTrackingQueryHandler(_readRepository)
            .Handle(new GetOrderTrackingQuery(orderId, attacker), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(OrderTrackingErrors.NotFound);
    }

    private static OrderTrackingSnapshot CreateSnapshot(Guid orderId, Guid customerUserId) => new(
        OrderId: orderId,
        CustomerUserId: customerUserId,
        MerchantId: Guid.NewGuid(),
        CurrentStatus: TrackingStatus.Placed,
        StatusReason: null,
        LastCourierId: null,
        LastCourierLat: null,
        LastCourierLng: null,
        LastLocationAtUtc: null,
        PickupLat: 18.48,
        PickupLng: -69.93,
        DeliveryLat: 18.49,
        DeliveryLng: -69.94,
        CreatedAtUtc: DateTime.UtcNow,
        UpdatedAtUtc: DateTime.UtcNow);
}
