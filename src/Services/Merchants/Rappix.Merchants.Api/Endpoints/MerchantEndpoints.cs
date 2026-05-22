using System.Security.Claims;
using MediatR;
using Rappix.BuildingBlocks.WebApi.Authentication;
using Rappix.BuildingBlocks.WebApi.Endpoints;
using Rappix.Merchants.Api.Contracts;
using Rappix.Merchants.Application.Merchants.AddServiceArea;
using Rappix.Merchants.Application.Merchants.GetMy;
using Rappix.Merchants.Application.Merchants.RemoveServiceArea;
using Rappix.Merchants.Application.Merchants.SubmitForApproval;
using Rappix.Merchants.Application.Merchants.UpdateOperatingHours;
using Rappix.Merchants.Application.Merchants.UpdateProfile;
using Rappix.Merchants.Application.Merchants.UploadLogo;

namespace Rappix.Merchants.Api.Endpoints;

/// <summary>Endpoints del comercio del owner autenticado bajo /merchants/me.</summary>
internal static class MerchantEndpoints
{
    private const long MaxLogoBytes = 2 * 1024 * 1024;

    public static RouteGroupBuilder MapMerchantEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder owner = group.MapGroup("/merchants")
            .WithTags("Merchants (owner)")
            .RequireAuthorization("RequireMerchant");

        owner.MapGet("/me", async (ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            return userId is null
                ? Results.Unauthorized()
                : (await sender.Send(new GetMyMerchantQuery(userId.Value), cancellationToken)).ToHttpResult();
        });

        owner.MapPut("/me", async (UpdateMerchantProfileRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var command = new UpdateMerchantProfileCommand(
                userId.Value, request.Name, request.Slug, request.Rnc, request.Description, request.VerticalType);
            return (await sender.Send(command, cancellationToken)).ToHttpResult();
        });

        owner.MapPut("/me/operating-hours", async (UpdateOperatingHoursRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            IReadOnlyList<OperatingHoursInput> hours =
                [.. request.Hours.Select(range => new OperatingHoursInput(range.DayOfWeek, range.OpensAt, range.ClosesAt))];
            return (await sender.Send(new UpdateOperatingHoursCommand(userId.Value, hours), cancellationToken)).ToHttpResult();
        });

        owner.MapPost("/me/service-areas", async (AddServiceAreaRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var command = new AddServiceAreaCommand(
                userId.Value, request.Type, request.Polygon, request.CenterLatitude, request.CenterLongitude, request.RadiusMeters);
            return (await sender.Send(command, cancellationToken)).ToHttpResult();
        });

        owner.MapDelete("/me/service-areas/{areaId:guid}", async (Guid areaId, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            return userId is null
                ? Results.Unauthorized()
                : (await sender.Send(new RemoveServiceAreaCommand(userId.Value, areaId), cancellationToken)).ToHttpResult();
        });

        owner.MapPost("/me/logo", async (IFormFile file, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            if (file is null || file.Length == 0)
            {
                return Results.Problem(detail: "El archivo de logo esta vacio.", statusCode: StatusCodes.Status400BadRequest);
            }

            if (file.Length > MaxLogoBytes)
            {
                return Results.Problem(detail: "El logo supera el tamano maximo de 2MB.", statusCode: StatusCodes.Status400BadRequest);
            }

            // Se bufferiza para garantizar un stream con seek (el validador y MinIO reposicionan el stream).
            await using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;

            return (await sender.Send(new UploadLogoCommand(userId.Value, buffer), cancellationToken)).ToHttpResult();
        }).DisableAntiforgery();

        owner.MapPost("/me/submit-for-approval", async (ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            return userId is null
                ? Results.Unauthorized()
                : (await sender.Send(new SubmitMerchantForApprovalCommand(userId.Value), cancellationToken)).ToHttpResult();
        }).RequireAuthorization("RequireConfirmedEmail");

        return group;
    }
}
