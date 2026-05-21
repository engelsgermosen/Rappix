using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Rappix.BuildingBlocks.WebApi.Endpoints;
using Rappix.Identity.Api.Authentication;
using Rappix.Identity.Api.Contracts;
using Rappix.Identity.Application.Authentication;
using Rappix.Identity.Application.Users.ConfirmEmail;
using Rappix.Identity.Application.Users.GetMe;
using Rappix.Identity.Application.Users.GoogleLogin;
using Rappix.Identity.Application.Users.Login;
using Rappix.Identity.Application.Users.Logout;
using Rappix.Identity.Application.Users.Refresh;
using Rappix.Identity.Application.Users.Register;
using Rappix.Identity.Application.Users.ResendConfirmation;

namespace Rappix.Identity.Api.Endpoints;

/// <summary>Endpoints de autenticacion bajo /auth.</summary>
internal static class AuthEndpoints
{
    private const string GoogleCallbackPath = "/api/v1/auth/google/callback";

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder auth = group.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/register", async (RegisterRequest request, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new RegisterCommand(request.Email, request.PhoneNumber, request.Password, request.FirstName, request.LastName, request.AccountType);
            return (await sender.Send(command, cancellationToken)).ToHttpResult();
        });

        auth.MapPost("/login", async (LoginRequest request, ISender sender, HttpContext http, CancellationToken cancellationToken) =>
        {
            var command = new LoginCommand(request.Identifier, request.Password, http.Connection.RemoteIpAddress?.ToString());
            return (await sender.Send(command, cancellationToken)).ToHttpResult();
        });

        auth.MapGet("/confirm-email", async (Guid userId, string token, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new ConfirmEmailCommand(userId, token), cancellationToken);
            return result.IsSuccess
                ? Results.Content(ConfirmationSuccessHtml, "text/html; charset=utf-8")
                : Results.Content(ConfirmationFailureHtml, "text/html; charset=utf-8", statusCode: StatusCodes.Status400BadRequest);
        });

        auth.MapPost("/resend-confirmation", async (ResendConfirmationRequest request, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new ResendConfirmationCommand(request.Email), cancellationToken)).ToHttpResult());

        auth.MapPost("/refresh", async (RefreshRequest request, ISender sender, HttpContext http, CancellationToken cancellationToken) =>
        {
            var command = new RefreshTokenCommand(request.RefreshToken, http.Connection.RemoteIpAddress?.ToString());
            return (await sender.Send(command, cancellationToken)).ToHttpResult();
        });

        auth.MapPost("/logout", async (LogoutRequest request, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new LogoutCommand(request.RefreshToken), cancellationToken)).ToHttpResult());

        auth.MapGet("/google", () =>
        {
            var properties = new AuthenticationProperties { RedirectUri = GoogleCallbackPath };
            return Results.Challenge(properties, [GoogleDefaults.AuthenticationScheme]);
        });

        auth.MapGet("/google/callback", async (IExternalAuthService externalAuth, ISender sender, HttpContext http, CancellationToken cancellationToken) =>
        {
            var externalUser = await externalAuth.GetExternalUserAsync(cancellationToken);
            if (externalUser.IsFailure)
            {
                return externalUser.ToHttpResult();
            }

            var command = new GoogleLoginCommand(externalUser.Value, http.Connection.RemoteIpAddress?.ToString());
            var result = await sender.Send(command, cancellationToken);

            // Limpia la cookie externa temporal antes de devolver el JWT propio.
            await http.SignOutAsync(ExternalAuthDefaults.Scheme);

            return result.ToHttpResult();
        });

        auth.MapGet("/me", async (System.Security.Claims.ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            return userId is null
                ? Results.Unauthorized()
                : (await sender.Send(new GetMeQuery(userId.Value), cancellationToken)).ToHttpResult();
        }).RequireAuthorization();

        return group;
    }

    private const string ConfirmationSuccessHtml =
        """
        <!DOCTYPE html><html lang="es"><head><meta charset="utf-8"><title>Email confirmado</title></head>
        <body style="font-family:Arial,sans-serif;text-align:center;padding:48px;color:#18181b;">
        <h1 style="color:#ff5a1f;">Rappix</h1><h2>Email confirmado</h2>
        <p>Tu cuenta ha sido verificada. Ya puedes iniciar sesion.</p></body></html>
        """;

    private const string ConfirmationFailureHtml =
        """
        <!DOCTYPE html><html lang="es"><head><meta charset="utf-8"><title>Enlace invalido</title></head>
        <body style="font-family:Arial,sans-serif;text-align:center;padding:48px;color:#18181b;">
        <h1 style="color:#ff5a1f;">Rappix</h1><h2>Enlace invalido o expirado</h2>
        <p>El enlace de confirmacion no es valido o ha expirado. Solicita uno nuevo.</p></body></html>
        """;
}
