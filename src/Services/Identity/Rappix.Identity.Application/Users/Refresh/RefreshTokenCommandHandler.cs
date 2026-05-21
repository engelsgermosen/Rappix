using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Identity.Application.Abstractions;
using Rappix.Identity.Application.Authentication;
using Rappix.Identity.Application.Responses;
using Rappix.Identity.Domain;
using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Application.Users.Refresh;

/// <summary>
/// Rota el refresh token presentado. Si se detecta reuso de un token ya revocado, el agregado
/// revoca todas las sesiones activas; aqui se registra el evento de seguridad y se persiste.
/// </summary>
internal sealed partial class RefreshTokenCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ITokenService tokenService,
    IDateTimeProvider clock,
    IOptions<JwtOptions> jwtOptions,
    ILogger<RefreshTokenCommandHandler> logger)
    : IRequestHandler<RefreshTokenCommand, Result<AuthResponse>>
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<Result<AuthResponse>> Handle(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        string presentedHash = tokenService.ComputeHash(command.RefreshToken);
        User? user = await users.GetByRefreshTokenHashAsync(presentedHash, cancellationToken);
        if (user is null)
        {
            return Result.Failure<AuthResponse>(TokenErrors.InvalidRefresh);
        }

        string rawRefresh = tokenService.GenerateOpaqueToken();
        Result rotation = user.RotateRefreshToken(
            presentedHash,
            tokenService.ComputeHash(rawRefresh),
            clock.UtcNow,
            TimeSpan.FromDays(_jwt.RefreshTokenLifetimeDays),
            command.IpAddress);

        if (rotation.IsFailure)
        {
            if (rotation.Error == TokenErrors.ReuseDetected)
            {
                LogRefreshTokenReuse(logger, user.Id.Value);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result.Failure<AuthResponse>(rotation.Error);
        }

        AccessToken accessToken = tokenService.CreateAccessToken(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResponse(accessToken.Token, accessToken.ExpiresAtUtc, rawRefresh, UserResponse.From(user));
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Reuso de refresh token detectado para el usuario {UserId}. Se revocaron todas las sesiones activas.")]
    private static partial void LogRefreshTokenReuse(ILogger logger, Guid userId);
}
