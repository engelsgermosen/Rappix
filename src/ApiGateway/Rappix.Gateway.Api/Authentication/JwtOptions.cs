namespace Rappix.Gateway.Api.Authentication;

/// <summary>
/// Opciones JWT: misma clave/issuer/audience que Identity para validar tokens en el gateway
/// (defensa en profundidad — los servicios destino tambien validan).
/// </summary>
public sealed class JwtOptions
{
    /// <summary>Nombre de la seccion en appsettings.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Issuer esperado (Identity).</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Audience esperada.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Clave simetrica de firma (compartida con Identity y los 8 servicios restantes).</summary>
    public string SigningKey { get; set; } = string.Empty;
}
