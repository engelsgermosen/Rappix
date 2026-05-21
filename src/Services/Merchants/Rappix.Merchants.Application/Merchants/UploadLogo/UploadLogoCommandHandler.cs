using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Application.Responses;
using Rappix.Merchants.Domain;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Merchants.UploadLogo;

/// <summary>Valida la imagen, la sube a MinIO y guarda la key en el merchant.</summary>
internal sealed class UploadLogoCommandHandler(
    IMerchantRepository merchants,
    IUnitOfWork unitOfWork,
    IObjectStorage storage,
    IImageValidator imageValidator,
    IDateTimeProvider clock)
    : IRequestHandler<UploadLogoCommand, Result<MerchantResponse>>
{
    public async Task<Result<MerchantResponse>> Handle(UploadLogoCommand command, CancellationToken cancellationToken)
    {
        Merchant? merchant = await merchants.GetByOwnerAsync(command.OwnerUserId, cancellationToken);
        if (merchant is null)
        {
            return Result.Failure<MerchantResponse>(MerchantErrors.NotFound);
        }

        command.Content.Position = 0;
        Result<string> validation = await imageValidator.ValidateAsync(command.Content, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<MerchantResponse>(validation.Error);
        }

        string contentType = validation.Value;
        string key = $"merchants/{merchant.Id.Value}/logo.{ExtensionFor(contentType)}";

        command.Content.Position = 0;
        await storage.UploadAsync(key, command.Content, contentType, cancellationToken);

        merchant.SetLogo(key, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MerchantResponse.From(merchant);
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "image/png" => "png",
        "image/jpeg" => "jpg",
        "image/webp" => "webp",
        _ => "bin",
    };
}
