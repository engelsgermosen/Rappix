using Microsoft.EntityFrameworkCore;
using Rappix.Identity.Application.Abstractions;
using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Infrastructure.Persistence.Repositories;

/// <summary>Implementacion EF Core del repositorio de usuarios.</summary>
internal sealed class UserRepository(IdentityDbContext context) : IUserRepository
{
    public void Add(User user) => context.Users.Add(user);

    public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken) =>
        context.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken) =>
        context.Users.FirstOrDefaultAsync(user => user.Email == email, cancellationToken);

    public Task<User?> GetByPhoneAsync(PhoneNumber phoneNumber, CancellationToken cancellationToken) =>
        context.Users.FirstOrDefaultAsync(user => user.PhoneNumber == phoneNumber, cancellationToken);

    public Task<bool> EmailExistsAsync(Email email, CancellationToken cancellationToken) =>
        context.Users.AnyAsync(user => user.Email == email, cancellationToken);

    public Task<bool> PhoneExistsAsync(PhoneNumber phoneNumber, CancellationToken cancellationToken) =>
        context.Users.AnyAsync(user => user.PhoneNumber == phoneNumber, cancellationToken);

    public Task<User?> GetByExternalLoginAsync(string provider, string providerKey, CancellationToken cancellationToken) =>
        context.Users
            .Include(user => user.ExternalLogins)
            .FirstOrDefaultAsync(
                user => user.ExternalLogins.Any(login => login.Provider == provider && login.ProviderKey == providerKey),
                cancellationToken);

    public Task<User?> GetByRefreshTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        context.Users
            .Include(user => user.RefreshTokens)
            .FirstOrDefaultAsync(user => user.RefreshTokens.Any(token => token.TokenHash == tokenHash), cancellationToken);

    public Task<User?> GetWithEmailTokensAsync(UserId id, CancellationToken cancellationToken) =>
        context.Users
            .Include(user => user.EmailConfirmationTokens)
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<User?> GetWithRefreshTokensAsync(UserId id, CancellationToken cancellationToken) =>
        context.Users
            .Include(user => user.RefreshTokens)
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
}
