using System.Security.Claims;

namespace Rappix.Identity.Api.Authentication;

/// <summary>Utilidades para extraer datos del usuario autenticado desde sus claims.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>Obtiene el identificador del usuario desde el claim "sub", o null si no es valido.</summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        string? sub = principal.FindFirstValue("sub");
        return Guid.TryParse(sub, out Guid userId) ? userId : null;
    }
}
