using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Responses;

namespace Rappix.Merchants.Application.Merchants.SetPickupLocation;

/// <summary>
/// Fija la ubicacion fisica del comercio (de donde recoge el courier). Lo necesita Dispatch (Fase 6)
/// para el matching geoespacial de couriers. Debe fijarse antes de enviar a aprobacion.
/// </summary>
public sealed record SetPickupLocationCommand(
    Guid OwnerUserId,
    double Latitude,
    double Longitude) : IRequest<Result<MerchantResponse>>;
