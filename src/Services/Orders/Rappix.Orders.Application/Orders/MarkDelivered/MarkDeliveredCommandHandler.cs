using MassTransit;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Contracts.Dispatch;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Domain.Errors;
using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Application.Orders.MarkDelivered;

/// <summary>Publica OrderDeliveredIntegrationEvent (seam de Dispatch). La saga lo consume si esta en InProgress.</summary>
internal sealed class MarkDeliveredCommandHandler(IOrderRepository orders, IPublishEndpoint publishEndpoint, IUnitOfWork unitOfWork)
    : IRequestHandler<MarkDeliveredCommand, Result>
{
    public async Task<Result> Handle(MarkDeliveredCommand command, CancellationToken cancellationToken)
    {
        Order? order = await orders.GetByIdAsync(new OrderId(command.OrderId), cancellationToken);
        if (order is null)
        {
            return Result.Failure(OrderErrors.NotFound);
        }

        await publishEndpoint.Publish(
            new OrderDeliveredIntegrationEvent { OrderId = command.OrderId, DeliveredAtUtc = DateTime.UtcNow },
            cancellationToken);
        // SaveChanges vacia el buffer del bus outbox (sin esto el OrderDelivered se pierde).
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
