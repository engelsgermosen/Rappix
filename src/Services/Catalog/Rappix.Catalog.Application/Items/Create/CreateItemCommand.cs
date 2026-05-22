using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Responses;

namespace Rappix.Catalog.Application.Items.Create;

/// <summary>Crea un item en el catalogo del merchant. Si TracksInventory, crea tambien su nivel de stock.</summary>
public sealed record CreateItemCommand(
    Guid MerchantId,
    Guid? CategoryId,
    string Name,
    string? Description,
    decimal PriceAmount,
    string? Currency,
    bool TracksInventory,
    int InitialStock,
    IReadOnlyDictionary<string, string>? Attributes) : IRequest<Result<ItemResponse>>;
