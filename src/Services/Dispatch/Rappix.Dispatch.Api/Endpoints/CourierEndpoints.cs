using System.Security.Claims;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.WebApi.Authentication;
using Rappix.BuildingBlocks.WebApi.Endpoints;
using Rappix.Dispatch.Api.Contracts;
using Rappix.Dispatch.Application.Couriers.Get;
using Rappix.Dispatch.Application.Couriers.GetCurrentAssignment;
using Rappix.Dispatch.Application.Couriers.GoOffline;
using Rappix.Dispatch.Application.Couriers.GoOnline;
using Rappix.Dispatch.Application.Couriers.MarkAssignmentDelivered;
using Rappix.Dispatch.Application.Couriers.ReportLocation;
using Rappix.Dispatch.Application.Couriers.UpdateVehicle;
using Rappix.Dispatch.Application.Responses;

namespace Rappix.Dispatch.Api.Endpoints;

/// <summary>Endpoints self-service del courier bajo /couriers/me. Todos requieren userType=Courier.</summary>
internal static class CourierEndpoints
{
    public static RouteGroupBuilder MapCourierEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder couriers = group.MapGroup("/couriers")
            .WithTags("Couriers (self-service)")
            .RequireAuthorization("RequireCourier");

        couriers.MapGet("/me", async (ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            return userId is null
                ? Results.Unauthorized()
                : (await sender.Send(new GetMyCourierQuery(userId.Value), cancellationToken)).ToHttpResult();
        });

        couriers.MapPut("/me/vehicle", async (UpdateVehicleRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var command = new UpdateVehicleCommand(userId.Value, request.VehicleType, request.Plate, request.CapacityKg);
            return (await sender.Send(command, cancellationToken)).ToHttpResult();
        });

        couriers.MapPost("/me/online", async (ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            return userId is null
                ? Results.Unauthorized()
                : (await sender.Send(new GoOnlineCommand(userId.Value), cancellationToken)).ToHttpResult();
        });

        couriers.MapPost("/me/offline", async (ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            return userId is null
                ? Results.Unauthorized()
                : (await sender.Send(new GoOfflineCommand(userId.Value), cancellationToken)).ToHttpResult();
        });

        couriers.MapPost("/me/location", async (ReportLocationRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var command = new ReportLocationCommand(userId.Value, request.Latitude, request.Longitude);
            return (await sender.Send(command, cancellationToken)).ToHttpResult();
        });

        couriers.MapGet("/me/current-assignment", async (ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            Result<CurrentAssignmentResponse?> result = await sender.Send(new GetCurrentAssignmentQuery(userId.Value), cancellationToken);
            if (result.IsFailure)
            {
                return result.ToHttpResult();
            }

            // 204 No Content si no hay asignacion activa.
            return result.Value is null ? Results.NoContent() : Results.Ok(result.Value);
        });

        // Fase 13.6: el courier marca su asignacion activa como entregada (publica OrderDelivered;
        // los consumers terminales liberan al courier y avanzan la saga). Sin body — ownership por
        // claim (resuelve la asignacion del JWT.sub, evita que el courier marque pedidos ajenos).
        couriers.MapPost("/me/current-assignment/delivered", async (ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            return userId is null
                ? Results.Unauthorized()
                : (await sender.Send(new MarkAssignmentDeliveredCommand(userId.Value), cancellationToken)).ToHttpResult();
        });

        return couriers;
    }
}
