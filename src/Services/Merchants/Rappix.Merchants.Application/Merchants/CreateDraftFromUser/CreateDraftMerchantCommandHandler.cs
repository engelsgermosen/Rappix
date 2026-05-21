using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Merchants.CreateDraftFromUser;

/// <summary>Crea el Draft con datos placeholder; idempotente (no duplica si ya existe para el owner).</summary>
internal sealed class CreateDraftMerchantCommandHandler(
    IMerchantRepository merchants,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<CreateDraftMerchantCommand, Result>
{
    public async Task<Result> Handle(CreateDraftMerchantCommand command, CancellationToken cancellationToken)
    {
        if (await merchants.ExistsByOwnerAsync(command.OwnerUserId, cancellationToken))
        {
            return Result.Success();
        }

        string name = string.IsNullOrWhiteSpace(command.OwnerFirstName)
            ? "Mi negocio"
            : $"Negocio de {command.OwnerFirstName.Trim()}";

        Slug slug = Slug.CreatePlaceholder(command.OwnerUserId);
        var merchant = Merchant.CreateDraft(command.OwnerUserId, name, slug, VerticalType.Food, CommissionPercentage.Default, clock.UtcNow);

        merchants.Add(merchant);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
