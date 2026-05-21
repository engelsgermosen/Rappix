using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Merchants.Application.Merchants.CreateDraftFromUser;

/// <summary>Crea un merchant Draft para un usuario Merchant. Invocado por el consumer (idempotente).</summary>
public sealed record CreateDraftMerchantCommand(Guid OwnerUserId, string OwnerFirstName) : IRequest<Result>;
