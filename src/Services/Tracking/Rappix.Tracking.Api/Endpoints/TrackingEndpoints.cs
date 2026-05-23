using System.Security.Claims;
using MediatR;
using Rappix.BuildingBlocks.WebApi.Authentication;
using Rappix.BuildingBlocks.WebApi.Endpoints;
using Rappix.Tracking.Application.OrderTrackings.GetOrderTracking;

namespace Rappix.Tracking.Api.Endpoints;

/// <summary>Endpoints REST bajo /tracking. Cualquier usuario autenticado consulta SUS pedidos.</summary>
internal static class TrackingEndpoints
{
    public static RouteGroupBuilder MapTrackingEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder tracking = group.MapGroup("/tracking")
            .WithTags("Tracking")
            .RequireAuthorization();

        // GET /api/v1/tracking/orders/{orderId} — snapshot actual del tracking (fallback REST cuando
        // el WebSocket no conecta, o warm-up antes del Subscribe del hub). Ownership por JWT.sub.
        // 404 y 403 indistinguibles (OrderTrackingErrors.NotFound cubre ambos).
        tracking.MapGet("/orders/{orderId:guid}", async (
            Guid orderId,
            ClaimsPrincipal principal,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            return userId is null
                ? Results.Unauthorized()
                : (await sender.Send(new GetOrderTrackingQuery(orderId, userId.Value), cancellationToken)).ToHttpResult();
        });

        return tracking;
    }
}
