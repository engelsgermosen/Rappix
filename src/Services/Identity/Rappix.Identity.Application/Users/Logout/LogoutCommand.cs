using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Identity.Application.Users.Logout;

/// <summary>Revoca el refresh token actual (cierre de sesion). Idempotente.</summary>
public sealed record LogoutCommand(string RefreshToken) : IRequest<Result>;
