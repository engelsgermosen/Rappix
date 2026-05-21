namespace Rappix.Identity.Application.Authentication;

/// <summary>Opciones de configuracion para la emision y validacion de JWT.</summary>
public sealed class JwtOptions
{
    /// <summary>Nombre de la seccion en la configuracion.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Emisor del token (iss).</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Audiencia del token (aud).</summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>Clave simetrica de firma (HS256). Minimo 256 bits. Nunca se commitea.</summary>
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>Vida del access token en minutos.</summary>
    public int AccessTokenLifetimeMinutes { get; init; } = 15;

    /// <summary>Vida del refresh token en dias.</summary>
    public int RefreshTokenLifetimeDays { get; init; } = 90;
}
