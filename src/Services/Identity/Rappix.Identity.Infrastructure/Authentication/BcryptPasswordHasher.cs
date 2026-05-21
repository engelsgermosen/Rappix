using Rappix.Identity.Application.Authentication;

namespace Rappix.Identity.Infrastructure.Authentication;

/// <summary>Hasher de contrasenas basado en BCrypt con work factor 12.</summary>
internal sealed class BcryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash) => BCrypt.Net.BCrypt.Verify(password, passwordHash);
}
