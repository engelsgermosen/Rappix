using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Application.Items.Attributes;
using Rappix.Catalog.Application.Responses;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Catalogs;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Items.Create;

/// <summary>
/// Crea un item: exige catalogo habilitado (gating cacheado), categoria valida y atributos validos
/// para el vertical. Si lleva inventario, crea su <see cref="StockLevel"/>. El item eleva ItemCreated.
/// </summary>
internal sealed class CreateItemCommandHandler(
    ICatalogRepository catalogs,
    IItemRepository items,
    IStockRepository stocks,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<CreateItemCommand, Result<ItemResponse>>
{
    public async Task<Result<ItemResponse>> Handle(CreateItemCommand command, CancellationToken cancellationToken)
    {
        MerchantCatalog? catalog = await catalogs.GetByMerchantAsync(command.MerchantId, cancellationToken);
        if (catalog is null)
        {
            return Result.Failure<ItemResponse>(CatalogErrors.NotFound);
        }

        if (!catalog.IsEnabled)
        {
            return Result.Failure<ItemResponse>(CatalogErrors.Disabled);
        }

        if (command.CategoryId is { } categoryId && !catalog.HasCategory(categoryId))
        {
            return Result.Failure<ItemResponse>(ItemErrors.CategoryNotInCatalog);
        }

        IReadOnlyDictionary<string, string> attributes = command.Attributes ?? EmptyAttributes;
        Result attributeValidation = VerticalAttributeRules.Validate(catalog.VerticalType, attributes);
        if (attributeValidation.IsFailure)
        {
            return Result.Failure<ItemResponse>(attributeValidation.Error);
        }

        Result<Money> price = Money.Create(command.PriceAmount, command.Currency);
        if (price.IsFailure)
        {
            return Result.Failure<ItemResponse>(price.Error);
        }

        Result<Item> item = Item.Create(
            command.MerchantId,
            command.CategoryId,
            command.Name,
            command.Description,
            price.Value,
            command.TracksInventory,
            attributes,
            clock.UtcNow);
        if (item.IsFailure)
        {
            return Result.Failure<ItemResponse>(item.Error);
        }

        items.Add(item.Value);

        if (command.TracksInventory)
        {
            Result<StockLevel> stock = StockLevel.Create(item.Value.Id, command.MerchantId, command.InitialStock, clock.UtcNow);
            if (stock.IsFailure)
            {
                return Result.Failure<ItemResponse>(stock.Error);
            }

            stocks.Add(stock.Value);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ItemResponse.From(item.Value);
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyAttributes =
        new Dictionary<string, string>();
}
