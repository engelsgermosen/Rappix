using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Identity.Application.Abstractions;
using Rappix.Identity.Application.Responses;
using Rappix.Identity.Domain;
using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Application.Users.UpdateProfile;

/// <summary>Actualiza el perfil verificando la unicidad del telefono si cambia.</summary>
internal sealed class UpdateProfileCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<UpdateProfileCommand, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> Handle(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(new UserId(command.UserId), cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserResponse>(UserErrors.NotFound);
        }

        PhoneNumber? phone = null;
        if (!string.IsNullOrWhiteSpace(command.PhoneNumber))
        {
            Result<PhoneNumber> phoneResult = PhoneNumber.Create(command.PhoneNumber);
            if (phoneResult.IsFailure)
            {
                return Result.Failure<UserResponse>(phoneResult.Error);
            }

            phone = phoneResult.Value;
            bool phoneChanged = user.PhoneNumber is null || user.PhoneNumber.Value != phone.Value;
            if (phoneChanged && await users.PhoneExistsAsync(phone, cancellationToken))
            {
                return Result.Failure<UserResponse>(UserErrors.PhoneInUse(phone.Value));
            }
        }

        user.UpdateProfile(command.FirstName, command.LastName, phone, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UserResponse.From(user);
    }
}
