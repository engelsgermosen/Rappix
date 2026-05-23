using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Tracking.Application.Abstractions;
using Rappix.Tracking.Application.OrderTrackings.Responses;

namespace Rappix.Tracking.Application.OrderTrackings.GetOrderTracking;

/// <summary>
/// Snapshot del tracking del pedido, autorizado por ownership (UserId del JWT vs
/// <c>OrderTracking.CustomerUserId</c>). 404 y 403 se devuelven con el mismo error
/// <see cref="OrderTrackingErrors.NotFound"/> para no filtrar existencia.
/// </summary>
public sealed record GetOrderTrackingQuery(Guid OrderId, Guid UserId) : IRequest<Result<OrderTrackingResponse>>;

/// <inheritdoc cref="GetOrderTrackingQuery" />
internal sealed class GetOrderTrackingQueryHandler(IOrderTrackingReadRepository readRepository)
    : IRequestHandler<GetOrderTrackingQuery, Result<OrderTrackingResponse>>
{
    public async Task<Result<OrderTrackingResponse>> Handle(GetOrderTrackingQuery query, CancellationToken cancellationToken)
    {
        OrderTrackingSnapshot? snapshot = await readRepository.GetSnapshotAsync(query.OrderId, cancellationToken);

        // 404 y 403 colapsados al mismo error: si el OrderId no existe O no es del usuario, mismo
        // mensaje. Evita que un atacante use el endpoint para enumerar pedidos ajenos.
        if (snapshot is null || snapshot.CustomerUserId != query.UserId)
        {
            return Result.Failure<OrderTrackingResponse>(OrderTrackingErrors.NotFound);
        }

        return OrderTrackingResponse.From(snapshot);
    }
}
