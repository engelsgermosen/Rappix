using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Identity.Application.Responses;

namespace Rappix.Identity.Application.Users.Refresh;

/// <summary>Renueva los tokens rotando el refresh token y detectando reuso (robo).</summary>
public sealed record RefreshTokenCommand(string RefreshToken, string? IpAddress) : IRequest<Result<AuthResponse>>;
