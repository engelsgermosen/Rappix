using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Responses;

namespace Rappix.Catalog.Application.Items.AddModifierOption;

/// <summary>Agrega una opcion a un grupo de modificadores de un item del merchant.</summary>
public sealed record AddModifierOptionCommand(
    Guid MerchantId,
    Guid ItemId,
    Guid ModifierId,
    string Name,
    decimal PriceDelta) : IRequest<Result<ItemResponse>>;
