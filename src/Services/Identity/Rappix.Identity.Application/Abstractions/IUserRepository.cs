using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Application.Abstractions;

/// <summary>Acceso a la persistencia del agregado User.</summary>
public interface IUserRepository
{
    /// <summary>Marca un nuevo usuario para insercion.</summary>
    void Add(User user);

    /// <summary>Obtiene un usuario por su Id (sin colecciones).</summary>
    Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken);

    /// <summary>Obtiene un usuario por email (sin colecciones).</summary>
    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken);

    /// <summary>Obtiene un usuario por telefono (sin colecciones).</summary>
    Task<User?> GetByPhoneAsync(PhoneNumber phoneNumber, CancellationToken cancellationToken);

    /// <summary>Indica si ya existe un usuario con ese email.</summary>
    Task<bool> EmailExistsAsync(Email email, CancellationToken cancellationToken);

    /// <summary>Indica si ya existe un usuario con ese telefono.</summary>
    Task<bool> PhoneExistsAsync(PhoneNumber phoneNumber, CancellationToken cancellationToken);

    /// <summary>Obtiene un usuario por un login externo, incluyendo sus logins externos.</summary>
    Task<User?> GetByExternalLoginAsync(string provider, string providerKey, CancellationToken cancellationToken);

    /// <summary>Obtiene al usuario propietario de un refresh token (por su hash), incluyendo todos sus refresh tokens.</summary>
    Task<User?> GetByRefreshTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Obtiene un usuario por Id incluyendo sus tokens de confirmacion de email.</summary>
    Task<User?> GetWithEmailTokensAsync(UserId id, CancellationToken cancellationToken);

    /// <summary>Obtiene un usuario por Id incluyendo sus refresh tokens.</summary>
    Task<User?> GetWithRefreshTokensAsync(UserId id, CancellationToken cancellationToken);
}
