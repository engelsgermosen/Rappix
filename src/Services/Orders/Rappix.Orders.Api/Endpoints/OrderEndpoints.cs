using System.Security.Claims;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.WebApi.Authentication;
using Rappix.BuildingBlocks.WebApi.Endpoints;
using Rappix.Orders.Api.Contracts;
using Rappix.Orders.Application.Orders.Cancel;
using Rappix.Orders.Application.Orders.Get;
using Rappix.Orders.Application.Orders.List;
using Rappix.Orders.Application.Orders.Place;
using Rappix.Orders.Application.Responses;

namespace Rappix.Orders.Api.Endpoints;

/// <summary>Endpoints del cliente: crear pedido, consultar y cancelar. Requieren JWT con userType=Customer.</summary>
internal static class OrderEndpoints
{
    public static RouteGroupBuilder MapOrderEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder orders = group.MapGroup("/orders")
            .WithTags("Orders")
            .RequireAuthorization("RequireCustomer");

        orders.MapPost("/", async (PlaceOrderRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var command = new PlaceOrderCommand(userId.Value, request.QuoteId, request.Street, request.Reference, request.Latitude, request.Longitude);
            Result<OrderResponse> result = await sender.Send(command, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/v1/orders/{result.Value.OrderId}", result.Value)
                : result.ToHttpResult();
        });

        orders.MapGet("/{orderId:guid}", async (Guid orderId, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            return userId is null
                ? Results.Unauthorized()
                : (await sender.Send(new GetOrderQuery(orderId, userId.Value), cancellationToken)).ToHttpResult();
        });

        orders.MapGet("/", async (ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken, int page = 1, int pageSize = 20) =>
        {
            Guid? userId = principal.GetUserId();
            return userId is null
                ? Results.Unauthorized()
                : (await sender.Send(new ListOrdersQuery(userId.Value, page, pageSize), cancellationToken)).ToHttpResult();
        });

        orders.MapPost("/{orderId:guid}/cancel", async (Guid orderId, CancelOrderRequest? request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            return userId is null
                ? Results.Unauthorized()
                : (await sender.Send(new CancelOrderCommand(orderId, userId.Value, request?.Reason), cancellationToken)).ToHttpResult();
        });

        return group;
    }
}
