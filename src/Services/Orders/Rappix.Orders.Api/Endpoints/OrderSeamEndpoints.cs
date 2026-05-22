using MediatR;
using Rappix.BuildingBlocks.WebApi.Endpoints;
using Rappix.Orders.Application.Orders.MarkDelivered;

namespace Rappix.Orders.Api.Endpoints;

/// <summary>
/// Endpoints SEAM TEMPORALES hacia Dispatch (Fase 6). Permiten marcar un pedido como entregado manualmente
/// para llevar la saga a Completed mientras no exista el servicio de Dispatch real. Eliminar en Fase 6.
/// </summary>
internal static class OrderSeamEndpoints
{
    public static RouteGroupBuilder MapOrderSeamEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder seam = group.MapGroup("/orders")
            .WithTags("Orders (dispatch seam, temporal)")
            .RequireAuthorization();

        seam.MapPost("/{orderId:guid}/mark-delivered", async (Guid orderId, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new MarkDeliveredCommand(orderId), cancellationToken)).ToHttpResult());

        return group;
    }
}
