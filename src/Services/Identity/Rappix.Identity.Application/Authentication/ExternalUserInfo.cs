namespace Rappix.Identity.Application.Authentication;

/// <summary>Datos de un usuario obtenidos de un proveedor de identidad externo (p.ej. Google).</summary>
public sealed record ExternalUserInfo(
    string Provider,
    string ProviderKey,
    string Email,
    string FirstName,
    string LastName);
