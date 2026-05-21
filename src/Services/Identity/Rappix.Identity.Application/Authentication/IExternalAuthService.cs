using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Identity.Application.Authentication;

/// <summary>
/// Extrae la informacion del usuario autenticado por un proveedor externo a partir del
/// resultado de autenticacion del esquema externo. Implementado en la capa Api (lee HttpContext);
/// se sustituye por un stub en las pruebas para no depender del proveedor real.
/// </summary>
public interface IExternalAuthService
{
    /// <summary>Obtiene los datos del usuario externo autenticado, o un fallo si no hay sesion externa.</summary>
    Task<Result<ExternalUserInfo>> GetExternalUserAsync(CancellationToken cancellationToken);
}
