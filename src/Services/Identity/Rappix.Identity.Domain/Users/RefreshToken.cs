using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Identity.Domain.Users;

/// <summary>
/// Refresh token persistido. Solo se almacena el hash SHA-256 del token opaco;
/// el valor en claro nunca toca la base de datos.
/// </summary>
public sealed class RefreshToken : Entity<Guid>
{
    private RefreshToken()
    {
    }

    internal RefreshToken(UserId userId, string tokenHash, DateTime createdAtUtc, DateTime expiresAtUtc, string? createdByIp)
        : base(Guid.CreateVersion7())
    {
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        CreatedByIp = createdByIp;
    }

    /// <summary>Usuario propietario del token.</summary>
    public UserId UserId { get; private set; }

    /// <summary>Hash SHA-256 (hex) del token opaco.</summary>
    public string TokenHash { get; private set; } = null!;

    /// <summary>Momento de creacion (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Momento de expiracion (UTC).</summary>
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>Momento de revocacion (UTC), si fue revocado.</summary>
    public DateTime? RevokedAtUtc { get; private set; }

    /// <summary>Id del token que reemplazo a este al rotar.</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    /// <summary>IP que origino la creacion del token, si se conoce.</summary>
    public string? CreatedByIp { get; private set; }

    /// <summary>Indica si el token fue revocado.</summary>
    public bool IsRevoked => RevokedAtUtc is not null;

    /// <summary>Indica si el token sigue activo (ni revocado ni expirado) en el momento dado.</summary>
    public bool IsActiveAt(DateTime utcNow) => RevokedAtUtc is null && ExpiresAtUtc > utcNow;

    internal void Revoke(DateTime utcNow) => RevokedAtUtc ??= utcNow;

    internal void Replace(Guid replacementId, DateTime utcNow)
    {
        RevokedAtUtc ??= utcNow;
        ReplacedByTokenId = replacementId;
    }
}
