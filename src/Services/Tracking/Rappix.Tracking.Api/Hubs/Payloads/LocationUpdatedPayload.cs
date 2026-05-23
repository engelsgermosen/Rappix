namespace Rappix.Tracking.Api.Hubs.Payloads;

/// <summary>
/// Payload del push <c>LocationUpdated</c> que el hub envia al grupo del pedido. Record inmutable
/// para que la serializacion JSON sea consistente con el resto del repo.
/// </summary>
/// <param name="Latitude">Latitud reportada por el courier.</param>
/// <param name="Longitude">Longitud reportada por el courier.</param>
/// <param name="ReportedAtUtc">Timestamp del reporte (UTC).</param>
/// <param name="CourierId">Courier asignado al pedido en este momento.</param>
public sealed record LocationUpdatedPayload(
    double Latitude,
    double Longitude,
    DateTime ReportedAtUtc,
    Guid CourierId);
