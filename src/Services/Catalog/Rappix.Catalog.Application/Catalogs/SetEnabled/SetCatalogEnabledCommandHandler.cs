using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Domain.Catalogs;

namespace Rappix.Catalog.Application.Catalogs.SetEnabled;

/// <summary>Aplica el gating al catalogo. Si aun no existe, es un no-op idempotente (lo creara MerchantApproved).</summary>
internal sealed class SetCatalogEnabledCommandHandler(
    ICatalogRepository catalogs,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<SetCatalogEnabledCommand, Result>
{
    public async Task<Result> Handle(SetCatalogEnabledCommand command, CancellationToken cancellationToken)
    {
        MerchantCatalog? catalog = await catalogs.GetByMerchantAsync(command.MerchantId, cancellationToken);
        if (catalog is null)
        {
            return Result.Success();
        }

        if (command.Enabled)
        {
            catalog.Enable(clock.UtcNow);
        }
        else
        {
            catalog.Disable(clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
