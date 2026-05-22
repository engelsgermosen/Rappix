using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Responses;

namespace Rappix.Catalog.Application.Items.AddModifier;

/// <summary>Agrega un grupo de modificadores a un item del merchant.</summary>
public sealed record AddModifierCommand(
    Guid MerchantId,
    Guid ItemId,
    string Name,
    bool IsRequired,
    int MinSelections,
    int MaxSelections) : IRequest<Result<ItemResponse>>;
