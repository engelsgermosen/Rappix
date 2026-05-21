namespace Rappix.Merchants.Api.Contracts;

/// <summary>Cuerpo de actualizacion del perfil del merchant.</summary>
public sealed record UpdateMerchantProfileRequest(
    string Name,
    string Slug,
    string? Rnc,
    string? Description,
    string VerticalType);

/// <summary>Un rango horario de entrada (dia + apertura/cierre en formato HH:mm).</summary>
public sealed record OperatingHoursRangeRequest(string DayOfWeek, string OpensAt, string ClosesAt);

/// <summary>Cuerpo de actualizacion bulk de horarios.</summary>
public sealed record UpdateOperatingHoursRequest(IReadOnlyList<OperatingHoursRangeRequest> Hours);

/// <summary>
/// Cuerpo para agregar una zona de cobertura. Si Type = "Polygon", se usa Polygon (anillo de pares
/// [longitud, latitud]); si Type = "Circle", se usan CenterLatitude + CenterLongitude + RadiusMeters.
/// </summary>
public sealed record AddServiceAreaRequest(
    string Type,
    IReadOnlyList<double[]>? Polygon,
    double? CenterLatitude,
    double? CenterLongitude,
    int? RadiusMeters);

/// <summary>Cuerpo del rechazo de un merchant (admin).</summary>
public sealed record RejectMerchantRequest(string Reason);

/// <summary>Cuerpo de la suspension de un merchant (admin).</summary>
public sealed record SuspendMerchantRequest(string Reason);

/// <summary>Cuerpo del ajuste de comision (admin).</summary>
public sealed record UpdateCommissionRequest(decimal CommissionPercentage);
