using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Identity.Application.Responses;

namespace Rappix.Identity.Application.Users.GetMe;

/// <summary>Obtiene el perfil del usuario autenticado.</summary>
public sealed record GetMeQuery(Guid UserId) : IRequest<Result<UserResponse>>;
