using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Responses;

namespace Rappix.Catalog.Application.Items.Update;

/// <summary>Actualiza nombre, descripcion, categoria y precio de un item del merchant.</summary>
public sealed record UpdateItemCommand(
    Guid MerchantId,
    Guid ItemId,
    Guid? CategoryId,
    string Name,
    string? Description,
    decimal PriceAmount,
    string? Currency) : IRequest<Result<ItemResponse>>;
