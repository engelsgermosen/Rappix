using MediatR;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Identity.Application.Abstractions;
using Rappix.Identity.Application.Authentication;
using Rappix.Identity.Application.Responses;
using Rappix.Identity.Domain;
using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Application.Users.GoogleLogin;

/// <summary>
/// Resuelve el usuario para un login con Google: lo busca por login externo, luego por email
/// (vinculando la cuenta) y, si no existe, lo crea con email ya confirmado.
/// </summary>
internal sealed class GoogleLoginCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ITokenService tokenService,
    IDateTimeProvider clock,
    IOptions<JwtOptions> jwtOptions)
    : IRequestHandler<GoogleLoginCommand, Result<AuthResponse>>
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<Result<AuthResponse>> Handle(GoogleLoginCommand command, CancellationToken cancellationToken)
    {
        ExternalUserInfo external = command.ExternalUser;
        DateTime now = clock.UtcNow;

        User? user = await users.GetByExternalLoginAsync(external.Provider, external.ProviderKey, cancellationToken);
        if (user is null)
        {
            Result<Email> emailResult = Email.Create(external.Email);
            if (emailResult.IsFailure)
            {
                return Result.Failure<AuthResponse>(emailResult.Error);
            }

            user = await users.GetByEmailAsync(emailResult.Value, cancellationToken);
            if (user is null)
            {
                user = User.RegisterWithGoogle(emailResult.Value, external.FirstName, external.LastName, external.ProviderKey, UserType.Customer, now);
                users.Add(user);
            }
            else
            {
                user.LinkExternalLogin(external.Provider, external.ProviderKey, external.Email, now);
            }
        }

        if (!user.IsActive)
        {
            return Result.Failure<AuthResponse>(UserErrors.Inactive);
        }

        AccessToken accessToken = tokenService.CreateAccessToken(user);
        string rawRefresh = tokenService.GenerateOpaqueToken();
        user.IssueRefreshToken(
            tokenService.ComputeHash(rawRefresh),
            now,
            TimeSpan.FromDays(_jwt.RefreshTokenLifetimeDays),
            command.IpAddress);
        user.RecordLogin();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResponse(accessToken.Token, accessToken.ExpiresAtUtc, rawRefresh, UserResponse.From(user));
    }
}
