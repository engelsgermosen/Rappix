using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Catalog.Application.Items.SetAvailability;

/// <summary>Publica u oculta un item del merchant.</summary>
public sealed record SetItemAvailabilityCommand(Guid MerchantId, Guid ItemId, bool Available) : IRequest<Result>;
