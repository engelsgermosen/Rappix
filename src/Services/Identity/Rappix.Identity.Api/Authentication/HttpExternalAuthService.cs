using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Identity.Application.Authentication;
using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Api.Authentication;

/// <summary>
/// Implementacion de IExternalAuthService que lee el principal autenticado por el esquema de
/// cookie externa (rellenado por el handler de Google tras el callback).
/// </summary>
internal sealed class HttpExternalAuthService(IHttpContextAccessor httpContextAccessor) : IExternalAuthService
{
    public async Task<Result<ExternalUserInfo>> GetExternalUserAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        HttpContext? httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return Result.Failure<ExternalUserInfo>(
                Error.Unauthorized("Identity.External.NoContext", "No hay contexto HTTP disponible."));
        }

        AuthenticateResult authentication = await httpContext.AuthenticateAsync(ExternalAuthDefaults.Scheme);
        if (!authentication.Succeeded || authentication.Principal is null)
        {
            return Result.Failure<ExternalUserInfo>(
                Error.Unauthorized("Identity.External.NotAuthenticated", "No hay una sesion externa valida."));
        }

        ClaimsPrincipal principal = authentication.Principal;
        string? providerKey = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        string? email = principal.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(email))
        {
            return Result.Failure<ExternalUserInfo>(
                Error.Unauthorized("Identity.External.MissingClaims", "El proveedor externo no devolvio los datos requeridos."));
        }

        string firstName = principal.FindFirstValue(ClaimTypes.GivenName) ?? string.Empty;
        string lastName = principal.FindFirstValue(ClaimTypes.Surname) ?? string.Empty;

        return new ExternalUserInfo(User.GoogleProvider, providerKey, email, firstName, lastName);
    }
}
