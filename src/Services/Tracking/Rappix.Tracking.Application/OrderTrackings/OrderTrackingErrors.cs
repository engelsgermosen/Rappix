using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Tracking.Application.OrderTrackings;

/// <summary>Errores del read model OrderTracking.</summary>
public static class OrderTrackingErrors
{
    /// <summary>
    /// Pedido no encontrado O no pertenece al usuario autenticado. El mismo error cubre 404 y 403
    /// para no filtrar existencia: si el cliente pregunta por un OrderId que no es suyo, recibe
    /// "no encontrado" igual que si no existiera.
    /// </summary>
    public static readonly Error NotFound =
        Error.NotFound("Tracking.OrderTracking.NotFound", "Tracking no encontrado.");
}
