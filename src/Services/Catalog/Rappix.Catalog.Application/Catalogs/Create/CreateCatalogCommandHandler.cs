using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Domain.Catalogs;

namespace Rappix.Catalog.Application.Catalogs.Create;

/// <summary>Crea el catalogo (habilitado) si el merchant no tiene uno; idempotente.</summary>
internal sealed class CreateCatalogCommandHandler(
    ICatalogRepository catalogs,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<CreateCatalogCommand, Result>
{
    public async Task<Result> Handle(CreateCatalogCommand command, CancellationToken cancellationToken)
    {
        if (await catalogs.ExistsByMerchantAsync(command.MerchantId, cancellationToken))
        {
            return Result.Success();
        }

        if (!Enum.TryParse(command.VerticalType, ignoreCase: true, out VerticalType vertical))
        {
            return Result.Failure(Error.Validation(
                "Catalog.Catalog.InvalidVertical",
                $"Vertical desconocido: {command.VerticalType}."));
        }

        MerchantCatalog catalog = MerchantCatalog.Create(command.MerchantId, command.OwnerUserId, vertical, clock.UtcNow);
        catalogs.Add(catalog);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
