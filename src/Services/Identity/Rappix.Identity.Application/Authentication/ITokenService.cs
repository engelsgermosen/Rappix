using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Application.Authentication;

/// <summary>Emite access tokens JWT y genera/hashea los tokens opacos (refresh y confirmacion).</summary>
public interface ITokenService
{
    /// <summary>Crea un access token JWT firmado para el usuario.</summary>
    AccessToken CreateAccessToken(User user);

    /// <summary>Genera un token opaco aleatorio (32 bytes en base64url) para enviar al cliente o por email.</summary>
    string GenerateOpaqueToken();

    /// <summary>Calcula el hash SHA-256 (hex en minusculas) de un token opaco, para almacenarlo o compararlo.</summary>
    string ComputeHash(string token);
}
