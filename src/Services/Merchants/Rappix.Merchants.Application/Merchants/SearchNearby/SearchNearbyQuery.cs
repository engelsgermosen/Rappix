using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Responses;

namespace Rappix.Merchants.Application.Merchants.SearchNearby;

/// <summary>Busca merchants Active cuya zona de cobertura contiene el punto dado.</summary>
public sealed record SearchNearbyQuery(
    double Latitude,
    double Longitude,
    string? Vertical,
    int Page,
    int PageSize) : IRequest<Result<IReadOnlyList<NearbyMerchantResponse>>>;
