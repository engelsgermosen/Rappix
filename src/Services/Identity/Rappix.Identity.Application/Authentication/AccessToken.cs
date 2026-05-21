namespace Rappix.Identity.Application.Authentication;

/// <summary>Access token JWT emitido junto a su instante de expiracion (UTC).</summary>
public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);
