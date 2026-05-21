using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Identity.Domain.Users;

/// <summary>Vinculacion de un proveedor de login externo (p.ej. Google) a un usuario.</summary>
public sealed class ExternalLogin : Entity<Guid>
{
    private ExternalLogin()
    {
    }

    internal ExternalLogin(UserId userId, string provider, string providerKey, string email, DateTime linkedAtUtc)
        : base(Guid.CreateVersion7())
    {
        UserId = userId;
        Provider = provider;
        ProviderKey = providerKey;
        Email = email;
        LinkedAtUtc = linkedAtUtc;
    }

    /// <summary>Usuario propietario de la vinculacion.</summary>
    public UserId UserId { get; private set; }

    /// <summary>Nombre del proveedor externo (p.ej. "Google").</summary>
    public string Provider { get; private set; } = null!;

    /// <summary>Identificador del usuario en el proveedor (claim "sub").</summary>
    public string ProviderKey { get; private set; } = null!;

    /// <summary>Email reportado por el proveedor.</summary>
    public string Email { get; private set; } = null!;

    /// <summary>Momento de vinculacion (UTC).</summary>
    public DateTime LinkedAtUtc { get; private set; }
}
