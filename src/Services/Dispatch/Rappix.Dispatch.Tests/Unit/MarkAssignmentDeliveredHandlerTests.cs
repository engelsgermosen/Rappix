using FluentAssertions;
using MassTransit;
using NSubstitute;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Dispatch;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Application.Couriers.MarkAssignmentDelivered;
using Rappix.Dispatch.Domain;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Tests.Unit;

/// <summary>
/// Pruebas unitarias del handler que el courier dispara al marcar entregado
/// (POST /api/v1/couriers/me/current-assignment/delivered). El handler NO libera la asignacion
/// directamente — solo publica OrderDeliveredIntegrationEvent y deja que el OrderTerminalEventsConsumer
/// haga el release. Los integration tests del flujo end-to-end viven en Integration/.
/// </summary>
public sealed class MarkAssignmentDeliveredHandlerTests
{
    private static readonly DateTime Now = new(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_NoActiveAssignment_Returns404Error()
    {
        Guid userId = Guid.CreateVersion7();
        ICourierAssignmentRepository assignments = Substitute.For<ICourierAssignmentRepository>();
        assignments.GetActiveByCourierAsync(Arg.Any<CourierId>(), Arg.Any<CancellationToken>())
            .Returns((CourierAssignment?)null);
        IPublishEndpoint publish = Substitute.For<IPublishEndpoint>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var handler = new MarkAssignmentDeliveredCommandHandler(assignments, publish, unitOfWork, clock);

        Result result = await handler.Handle(new MarkAssignmentDeliveredCommand(userId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CourierAssignmentErrors.NoActiveAssignment);

        // No se publico evento ni se llamo SaveChanges si la asignacion no existe.
        await publish.DidNotReceive().Publish(Arg.Any<OrderDeliveredIntegrationEvent>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ActiveAssignment_PublishesOrderDeliveredWithOrderId_AndSavesChanges()
    {
        Guid userId = Guid.CreateVersion7();
        Guid orderId = Guid.CreateVersion7();
        CourierAssignment assignment = CourierAssignment.Create(
            CourierId.FromUserId(userId), orderId, AssignmentSnapshot.Empty, Now.AddMinutes(-5));

        ICourierAssignmentRepository assignments = Substitute.For<ICourierAssignmentRepository>();
        assignments.GetActiveByCourierAsync(Arg.Any<CourierId>(), Arg.Any<CancellationToken>())
            .Returns(assignment);
        IPublishEndpoint publish = Substitute.For<IPublishEndpoint>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var handler = new MarkAssignmentDeliveredCommandHandler(assignments, publish, unitOfWork, clock);

        Result result = await handler.Handle(new MarkAssignmentDeliveredCommand(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // Verifica que se publico el evento con el orderId de la asignacion del courier y el reloj del handler.
        await publish.Received(1).Publish(
            Arg.Is<OrderDeliveredIntegrationEvent>(e => e.OrderId == orderId && e.DeliveredAtUtc == Now),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
