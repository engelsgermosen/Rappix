using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Catalogs;

namespace Rappix.Catalog.Application.Catalogs.RemoveCategory;

/// <summary>Elimina una categoria existente.</summary>
internal sealed class RemoveCategoryCommandHandler(
    ICatalogRepository catalogs,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<RemoveCategoryCommand, Result>
{
    public async Task<Result> Handle(RemoveCategoryCommand command, CancellationToken cancellationToken)
    {
        MerchantCatalog? catalog = await catalogs.GetByMerchantAsync(command.MerchantId, cancellationToken);
        if (catalog is null)
        {
            return Result.Failure(CatalogErrors.NotFound);
        }

        Result removal = catalog.RemoveCategory(command.CategoryId, clock.UtcNow);
        if (removal.IsFailure)
        {
            return removal;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
