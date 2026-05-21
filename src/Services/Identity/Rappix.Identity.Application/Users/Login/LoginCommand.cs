using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Identity.Application.Responses;

namespace Rappix.Identity.Application.Users.Login;

/// <summary>Login local. El identificador puede ser email o telefono.</summary>
public sealed record LoginCommand(
    string Identifier,
    string Password,
    string? IpAddress) : IRequest<Result<AuthResponse>>;
