using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Identity.Application.Abstractions;
using Rappix.Identity.Application.Authentication;
using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Application.Users.Logout;

/// <summary>Revoca el refresh token presentado; si no existe, responde de forma idempotente.</summary>
internal sealed class LogoutCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ITokenService tokenService,
    IDateTimeProvider clock)
    : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        string presentedHash = tokenService.ComputeHash(command.RefreshToken);
        User? user = await users.GetByRefreshTokenHashAsync(presentedHash, cancellationToken);
        if (user is null)
        {
            return Result.Success();
        }

        user.RevokeRefreshToken(presentedHash, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
