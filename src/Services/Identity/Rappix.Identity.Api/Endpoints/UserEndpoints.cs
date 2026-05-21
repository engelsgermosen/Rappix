using MediatR;
using Rappix.BuildingBlocks.WebApi.Endpoints;
using Rappix.Identity.Api.Authentication;
using Rappix.Identity.Api.Contracts;
using Rappix.Identity.Application.Users.ChangePassword;
using Rappix.Identity.Application.Users.UpdateProfile;

namespace Rappix.Identity.Api.Endpoints;

/// <summary>Endpoints de gestion del usuario autenticado bajo /users.</summary>
internal static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder users = group.MapGroup("/users").WithTags("Users").RequireAuthorization();

        users.MapPut("/me", async (UpdateProfileRequest request, System.Security.Claims.ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var command = new UpdateProfileCommand(userId.Value, request.FirstName, request.LastName, request.PhoneNumber);
            return (await sender.Send(command, cancellationToken)).ToHttpResult();
        });

        users.MapPost("/me/change-password", async (ChangePasswordRequest request, System.Security.Claims.ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var command = new ChangePasswordCommand(userId.Value, request.CurrentPassword, request.NewPassword);
            return (await sender.Send(command, cancellationToken)).ToHttpResult();
        });

        return group;
    }
}
