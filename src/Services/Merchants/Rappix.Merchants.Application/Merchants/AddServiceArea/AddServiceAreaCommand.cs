using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Responses;

namespace Rappix.Merchants.Application.Merchants.AddServiceArea;

/// <summary>
/// Agrega una zona de cobertura. Si Type = "Polygon", se usa <see cref="Polygon"/> (anillo de
/// pares [longitud, latitud]); si Type = "Circle", se usan centro + radio.
/// </summary>
public sealed record AddServiceAreaCommand(
    Guid OwnerUserId,
    string Type,
    IReadOnlyList<double[]>? Polygon,
    double? CenterLatitude,
    double? CenterLongitude,
    int? RadiusMeters) : IRequest<Result<MerchantResponse>>;
