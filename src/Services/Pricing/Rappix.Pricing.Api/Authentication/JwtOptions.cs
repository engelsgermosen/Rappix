namespace Rappix.Pricing.Api.Authentication;

/// <summary>Opciones de validacion del JWT. Los tokens los emite Identity; Pricing solo los valida.</summary>
public sealed class JwtOptions
{
    /// <summary>Nombre de la seccion de configuracion.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Emisor esperado (debe coincidir con el de Identity).</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Audiencia esperada.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Clave simetrica HS256 compartida con Identity.</summary>
    public string SigningKey { get; set; } = string.Empty;
}
