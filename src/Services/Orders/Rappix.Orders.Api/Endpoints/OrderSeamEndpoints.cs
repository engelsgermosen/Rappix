using MediatR;
using Rappix.BuildingBlocks.WebApi.Endpoints;
using Rappix.Orders.Application.Orders.MarkDelivered;

namespace Rappix.Orders.Api.Endpoints;

/// <summary>
/// Endpoints SEAM hacia Dispatch. Marca un pedido como entregado manualmente para llevar la saga a
/// Completed. A partir de Fase 13.6 hay un endpoint courier real con ownership en Dispatch
/// (<c>POST /api/v1/couriers/me/current-assignment/delivered</c>); este seam queda vivo SOLO para
/// <c>tools/smoke-tracking-e2e.ps1</c> y <c>tools/seed-smoke.ps1</c> mientras esos scripts no migren
/// al endpoint courier. Sin ownership (<c>RequireAuthorization()</c> a secas) — NO usar desde portales
/// reales. Follow-up: borrar este seam cuando los smokes esten migrados.
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
