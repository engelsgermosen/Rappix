using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Identity.Application.Abstractions;
using Rappix.Identity.Application.Authentication;
using Rappix.Identity.Domain;
using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Application.Users.ConfirmEmail;

/// <summary>Valida el token de confirmacion (por hash) y marca el email como confirmado.</summary>
internal sealed class ConfirmEmailCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ITokenService tokenService,
    IDateTimeProvider clock)
    : IRequestHandler<ConfirmEmailCommand, Result>
{
    public async Task<Result> Handle(ConfirmEmailCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetWithEmailTokensAsync(new UserId(command.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure(TokenErrors.InvalidConfirmation);
        }

        Result confirmation = user.ConfirmEmail(tokenService.ComputeHash(command.Token), clock.UtcNow);
        if (confirmation.IsFailure)
        {
            return confirmation;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
