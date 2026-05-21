using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Identity.Domain.Users;

/// <summary>
/// Token de confirmacion de email. Solo se almacena el hash SHA-256 del token opaco
/// enviado por correo; expira a las 24 horas.
/// </summary>
public sealed class EmailConfirmationToken : Entity<Guid>
{
    private EmailConfirmationToken()
    {
    }

    internal EmailConfirmationToken(UserId userId, string tokenHash, DateTime createdAtUtc, DateTime expiresAtUtc)
        : base(Guid.CreateVersion7())
    {
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>Usuario propietario del token.</summary>
    public UserId UserId { get; private set; }

    /// <summary>Hash SHA-256 (hex) del token opaco.</summary>
    public string TokenHash { get; private set; } = null!;

    /// <summary>Momento de creacion (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Momento de expiracion (UTC).</summary>
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>Momento de uso (UTC), si ya fue consumido.</summary>
    public DateTime? UsedAtUtc { get; private set; }

    /// <summary>Indica si el token ya fue usado.</summary>
    public bool IsUsed => UsedAtUtc is not null;

    /// <summary>Indica si el token sigue valido (ni usado ni expirado) en el momento dado.</summary>
    public bool IsValidAt(DateTime utcNow) => UsedAtUtc is null && ExpiresAtUtc > utcNow;

    internal void MarkUsed(DateTime utcNow) => UsedAtUtc ??= utcNow;
}
