using System.Security.Claims;
using MediatR;
using Rappix.BuildingBlocks.WebApi.Authentication;
using Rappix.BuildingBlocks.WebApi.Endpoints;
using Rappix.Orders.Api.Contracts;
using Rappix.Orders.Application.Orders.Accept;
using Rappix.Orders.Application.Orders.MerchantPending;
using Rappix.Orders.Application.Orders.Reject;

namespace Rappix.Orders.Api.Endpoints;

/// <summary>
/// Endpoints del merchant: ver pedidos pendientes, aceptar y rechazar. Requieren JWT con userType=Merchant.
/// El identificador del merchant se toma del sub del JWT (simplificacion de fase: en un sistema real se
/// resolveria la membresia usuario -> merchant).
/// </summary>
internal static class MerchantOrderEndpoints
{
    public static RouteGroupBuilder MapMerchantOrderEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder merchant = group.MapGroup("/orders")
            .WithTags("Orders (merchant)")
            .RequireAuthorization("RequireMerchant");

        merchant.MapGet("/merchant/pending", async (ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken, int page = 1, int pageSize = 20) =>
        {
            Guid? merchantUserId = principal.GetUserId();
            return merchantUserId is null
                ? Results.Unauthorized()
                : (await sender.Send(new ListMerchantPendingQuery(merchantUserId.Value, page, pageSize), cancellationToken)).ToHttpResult();
        });

        merchant.MapPost("/{orderId:guid}/accept", async (Guid orderId, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? merchantUserId = principal.GetUserId();
            return merchantUserId is null
                ? Results.Unauthorized()
                : (await sender.Send(new AcceptOrderCommand(orderId, merchantUserId.Value), cancellationToken)).ToHttpResult();
        });

        merchant.MapPost("/{orderId:guid}/reject", async (Guid orderId, RejectOrderRequest? request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? merchantUserId = principal.GetUserId();
            return merchantUserId is null
                ? Results.Unauthorized()
                : (await sender.Send(new RejectOrderCommand(orderId, merchantUserId.Value, request?.Reason ?? "Rechazado por el merchant"), cancellationToken)).ToHttpResult();
        });

        return group;
    }
}
