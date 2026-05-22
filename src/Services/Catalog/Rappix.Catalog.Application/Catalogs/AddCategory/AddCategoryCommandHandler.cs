using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Application.Responses;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Catalogs;

namespace Rappix.Catalog.Application.Catalogs.AddCategory;

/// <summary>Agrega una categoria validando unicidad de nombre dentro del catalogo.</summary>
internal sealed class AddCategoryCommandHandler(
    ICatalogRepository catalogs,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<AddCategoryCommand, Result<CategoryResponse>>
{
    public async Task<Result<CategoryResponse>> Handle(AddCategoryCommand command, CancellationToken cancellationToken)
    {
        MerchantCatalog? catalog = await catalogs.GetByMerchantAsync(command.MerchantId, cancellationToken);
        if (catalog is null)
        {
            return Result.Failure<CategoryResponse>(CatalogErrors.NotFound);
        }

        Result<Category> category = catalog.AddCategory(command.Name, command.SortOrder, clock.UtcNow);
        if (category.IsFailure)
        {
            return Result.Failure<CategoryResponse>(category.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CategoryResponse.From(category.Value);
    }
}
