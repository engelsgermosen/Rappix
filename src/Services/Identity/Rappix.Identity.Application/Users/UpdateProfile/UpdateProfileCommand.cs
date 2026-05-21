using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Identity.Application.Responses;

namespace Rappix.Identity.Application.Users.UpdateProfile;

/// <summary>Actualiza el perfil del usuario autenticado (nombre, apellido y telefono).</summary>
public sealed record UpdateProfileCommand(
    Guid UserId,
    string FirstName,
    string LastName,
    string? PhoneNumber) : IRequest<Result<UserResponse>>;
