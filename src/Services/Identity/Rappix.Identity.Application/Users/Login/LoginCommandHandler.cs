using MediatR;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Identity.Application.Abstractions;
using Rappix.Identity.Application.Authentication;
using Rappix.Identity.Application.Responses;
using Rappix.Identity.Domain;
using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Application.Users.Login;

/// <summary>Verifica credenciales y emite access token + refresh token.</summary>
internal sealed class LoginCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IDateTimeProvider clock,
    IOptions<JwtOptions> jwtOptions)
    : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<Result<AuthResponse>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        User? user = await ResolveUserAsync(command.Identifier, cancellationToken);
        if (user is null || user.PasswordHash is null || !passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            return Result.Failure<AuthResponse>(UserErrors.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return Result.Failure<AuthResponse>(UserErrors.Inactive);
        }

        AccessToken accessToken = tokenService.CreateAccessToken(user);
        string rawRefresh = tokenService.GenerateOpaqueToken();
        user.IssueRefreshToken(
            tokenService.ComputeHash(rawRefresh),
            clock.UtcNow,
            TimeSpan.FromDays(_jwt.RefreshTokenLifetimeDays),
            command.IpAddress);
        user.RecordLogin();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResponse(accessToken.Token, accessToken.ExpiresAtUtc, rawRefresh, UserResponse.From(user));
    }

    private async Task<User?> ResolveUserAsync(string identifier, CancellationToken cancellationToken)
    {
        Result<Email> email = Email.Create(identifier);
        if (email.IsSuccess)
        {
            return await users.GetByEmailAsync(email.Value, cancellationToken);
        }

        Result<PhoneNumber> phone = PhoneNumber.Create(identifier);
        if (phone.IsSuccess)
        {
            return await users.GetByPhoneAsync(phone.Value, cancellationToken);
        }

        return null;
    }
}
