using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Identity.Application.Abstractions;
using Rappix.Identity.Application.Authentication;
using Rappix.Identity.Domain;
using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Application.Users.ChangePassword;

/// <summary>Verifica la contrasena actual y establece la nueva, revocando las sesiones activas.</summary>
internal sealed class ChangePasswordCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IDateTimeProvider clock)
    : IRequestHandler<ChangePasswordCommand, Result>
{
    public async Task<Result> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetWithRefreshTokensAsync(new UserId(command.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        if (user.PasswordHash is null)
        {
            return Result.Failure(UserErrors.NoPasswordSet);
        }

        if (!passwordHasher.Verify(command.CurrentPassword, user.PasswordHash))
        {
            return Result.Failure(UserErrors.InvalidCredentials);
        }

        user.ChangePassword(passwordHasher.Hash(command.NewPassword), clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
