using MediatR;
using NetTopologySuite.Geometries;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Application.Geo;
using Rappix.Merchants.Application.Responses;
using Rappix.Merchants.Domain;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Merchants.SetPickupLocation;

/// <summary>Construye el Point (SRID 4326, axis order X=lng/Y=lat) y delega al agregado.</summary>
internal sealed class SetPickupLocationCommandHandler(
    IMerchantRepository merchants,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<SetPickupLocationCommand, Result<MerchantResponse>>
{
    public async Task<Result<MerchantResponse>> Handle(SetPickupLocationCommand command, CancellationToken cancellationToken)
    {
        Merchant? merchant = await merchants.GetByOwnerAsync(command.OwnerUserId, cancellationToken);
        if (merchant is null)
        {
            return Result.Failure<MerchantResponse>(MerchantErrors.NotFound);
        }

        Point pickup = GeoFactory.CreatePoint(command.Latitude, command.Longitude);
        Result set = merchant.SetPickupLocation(pickup, clock.UtcNow);
        if (set.IsFailure)
        {
            return Result.Failure<MerchantResponse>(set.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MerchantResponse.From(merchant);
    }
}
