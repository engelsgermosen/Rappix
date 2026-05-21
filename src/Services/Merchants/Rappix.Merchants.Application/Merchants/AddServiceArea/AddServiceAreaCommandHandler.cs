using MediatR;
using NetTopologySuite.Geometries;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Application.Geo;
using Rappix.Merchants.Application.Responses;
using Rappix.Merchants.Domain;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Merchants.AddServiceArea;

/// <summary>Construye la geometria (SRID 4326) y agrega la zona al merchant del owner.</summary>
internal sealed class AddServiceAreaCommandHandler(
    IMerchantRepository merchants,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<AddServiceAreaCommand, Result<MerchantResponse>>
{
    public async Task<Result<MerchantResponse>> Handle(AddServiceAreaCommand command, CancellationToken cancellationToken)
    {
        Merchant? merchant = await merchants.GetByOwnerAsync(command.OwnerUserId, cancellationToken);
        if (merchant is null)
        {
            return Result.Failure<MerchantResponse>(MerchantErrors.NotFound);
        }

        DateTime now = clock.UtcNow;
        var type = Enum.Parse<ServiceAreaType>(command.Type, ignoreCase: true);

        Result added = type == ServiceAreaType.Polygon
            ? merchant.AddPolygonServiceArea(GeoFactory.CreatePolygon(command.Polygon!), now)
            : merchant.AddCircleServiceArea(GeoFactory.CreatePoint(command.CenterLatitude!.Value, command.CenterLongitude!.Value), command.RadiusMeters!.Value, now);

        if (added.IsFailure)
        {
            return Result.Failure<MerchantResponse>(added.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MerchantResponse.From(merchant);
    }
}
