namespace Rappix.Identity.Application.Responses;

/// <summary>Respuesta de autenticacion con el access token, su expiracion y el refresh token.</summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    UserResponse User);
