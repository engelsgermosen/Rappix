using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Identity.Application.Users.ResendConfirmation;

/// <summary>Reenvia el email de confirmacion. Responde de forma generica para no revelar si el email existe.</summary>
public sealed record ResendConfirmationCommand(string Email) : IRequest<Result>;
