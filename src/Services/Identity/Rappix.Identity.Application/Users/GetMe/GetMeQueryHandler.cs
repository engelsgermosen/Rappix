using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Identity.Application.Abstractions;
using Rappix.Identity.Application.Responses;
using Rappix.Identity.Domain;
using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Application.Users.GetMe;

/// <summary>Devuelve el perfil del usuario o NotFound.</summary>
internal sealed class GetMeQueryHandler(IUserRepository users)
    : IRequestHandler<GetMeQuery, Result<UserResponse>>
{
    public async Task<Result<UserResponse>> Handle(GetMeQuery query, CancellationToken cancellationToken)
    {
        User? user = await users.GetByIdAsync(new UserId(query.UserId), cancellationToken);
        return user is null
            ? Result.Failure<UserResponse>(UserErrors.NotFound)
            : UserResponse.From(user);
    }
}
