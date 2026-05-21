using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Identity.Application.Authentication;
using Rappix.Identity.Application.Responses;

namespace Rappix.Identity.Application.Users.GoogleLogin;

/// <summary>Inicia sesion via Google: busca, vincula o crea el usuario y emite tokens.</summary>
public sealed record GoogleLoginCommand(ExternalUserInfo ExternalUser, string? IpAddress) : IRequest<Result<AuthResponse>>;
