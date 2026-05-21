using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Identity.Application.Users.ConfirmEmail;

/// <summary>Confirma el email de un usuario a partir del userId y el token recibidos por enlace.</summary>
public sealed record ConfirmEmailCommand(Guid UserId, string Token) : IRequest<Result>;
