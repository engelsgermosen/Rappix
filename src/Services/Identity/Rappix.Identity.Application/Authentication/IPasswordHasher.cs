namespace Rappix.Identity.Application.Authentication;

/// <summary>Hashea y verifica contrasenas (BCrypt).</summary>
public interface IPasswordHasher
{
    /// <summary>Hashea una contrasena en claro.</summary>
    string Hash(string password);

    /// <summary>Verifica una contrasena en claro contra su hash.</summary>
    bool Verify(string password, string passwordHash);
}
