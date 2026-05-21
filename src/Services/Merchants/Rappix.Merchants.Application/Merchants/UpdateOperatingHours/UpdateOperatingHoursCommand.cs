using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Responses;

namespace Rappix.Merchants.Application.Merchants.UpdateOperatingHours;

/// <summary>Un rango horario de entrada (dia + apertura/cierre en formato HH:mm).</summary>
public sealed record OperatingHoursInput(string DayOfWeek, string OpensAt, string ClosesAt);

/// <summary>Reemplaza todos los horarios del merchant del owner.</summary>
public sealed record UpdateOperatingHoursCommand(Guid OwnerUserId, IReadOnlyList<OperatingHoursInput> Hours)
    : IRequest<Result<MerchantResponse>>;
