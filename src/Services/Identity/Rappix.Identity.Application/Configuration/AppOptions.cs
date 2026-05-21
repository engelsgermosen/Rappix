namespace Rappix.Identity.Application.Configuration;

/// <summary>Opciones generales de la aplicacion (URLs publicas para construir enlaces).</summary>
public sealed class AppOptions
{
    /// <summary>Nombre de la seccion en la configuracion.</summary>
    public const string SectionName = "App";

    /// <summary>URL base publica del backend (para construir el enlace de confirmacion de email).</summary>
    public string PublicBaseUrl { get; init; } = "https://localhost:5001";

    /// <summary>Ruta del endpoint de confirmacion de email.</summary>
    public string EmailConfirmationPath { get; init; } = "/api/v1/auth/confirm-email";

    /// <summary>URL del frontend (reservada para fases futuras).</summary>
    public string FrontendUrl { get; init; } = "http://localhost:3000";

    /// <summary>Construye el enlace de confirmacion de email apuntando al backend.</summary>
    public string BuildEmailConfirmationUrl(Guid userId, string rawToken) =>
        $"{PublicBaseUrl.TrimEnd('/')}{EmailConfirmationPath}?userId={userId}&token={Uri.EscapeDataString(rawToken)}";
}
