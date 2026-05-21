namespace Rappix.Identity.Api.Authentication;

/// <summary>Constantes del esquema de autenticacion externa (cookie temporal del handshake OAuth).</summary>
public static class ExternalAuthDefaults
{
    /// <summary>Nombre del esquema de cookie usado como SignInScheme del proveedor externo.</summary>
    public const string Scheme = "External";
}
