using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Responses;

namespace Rappix.Catalog.Application.Items.SetAttributes;

/// <summary>Reemplaza los atributos por vertical de un item (validados contra las reglas del vertical).</summary>
public sealed record SetItemAttributesCommand(
    Guid MerchantId,
    Guid ItemId,
    IReadOnlyDictionary<string, string> Attributes) : IRequest<Result<ItemResponse>>;
