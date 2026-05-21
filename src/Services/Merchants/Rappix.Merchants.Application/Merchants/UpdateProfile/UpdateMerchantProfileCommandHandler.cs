using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Application.Responses;
using Rappix.Merchants.Domain;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Merchants.UpdateProfile;

/// <summary>Actualiza el perfil validando unicidad de slug y RNC.</summary>
internal sealed class UpdateMerchantProfileCommandHandler(
    IMerchantRepository merchants,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<UpdateMerchantProfileCommand, Result<MerchantResponse>>
{
    public async Task<Result<MerchantResponse>> Handle(UpdateMerchantProfileCommand command, CancellationToken cancellationToken)
    {
        Merchant? merchant = await merchants.GetByOwnerAsync(command.OwnerUserId, cancellationToken);
        if (merchant is null)
        {
            return Result.Failure<MerchantResponse>(MerchantErrors.NotFound);
        }

        Result<Slug> slugResult = Slug.Create(command.Slug);
        if (slugResult.IsFailure)
        {
            return Result.Failure<MerchantResponse>(slugResult.Error);
        }

        Rnc? rnc = null;
        if (!string.IsNullOrWhiteSpace(command.Rnc))
        {
            Result<Rnc> rncResult = Rnc.Create(command.Rnc);
            if (rncResult.IsFailure)
            {
                return Result.Failure<MerchantResponse>(rncResult.Error);
            }

            rnc = rncResult.Value;
        }

        Slug slug = slugResult.Value;
        if (await merchants.SlugExistsAsync(slug.Value, merchant.Id, cancellationToken))
        {
            return Result.Failure<MerchantResponse>(MerchantErrors.SlugInUse(slug.Value));
        }

        if (rnc is not null && await merchants.RncExistsAsync(rnc.Value, merchant.Id, cancellationToken))
        {
            return Result.Failure<MerchantResponse>(MerchantErrors.RncInUse(rnc.Value));
        }

        var vertical = Enum.Parse<VerticalType>(command.VerticalType, ignoreCase: true);

        Result update = merchant.UpdateProfile(command.Name, slug, rnc, command.Description, vertical, clock.UtcNow);
        if (update.IsFailure)
        {
            return Result.Failure<MerchantResponse>(update.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MerchantResponse.From(merchant);
    }
}
