using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Identity.Application.Users.ChangePassword;

/// <summary>Cambia la contrasena del usuario autenticado. Revoca todas las sesiones activas.</summary>
public sealed record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword) : IRequest<Result>;
