using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Dispatch.Application.Couriers.CreateDraftFromUser;

/// <summary>
/// Crea un courier Draft (Offline, sin vehiculo, sin location) a partir de UserRegistered. Idempotente:
/// si ya existe, devuelve Success. El courier completara su perfil via REST en commit 9.
/// </summary>
public sealed record CreateDraftCourierCommand(Guid UserId, string FirstName) : IRequest<Result>;
