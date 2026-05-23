using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Notifications.Application.Notifications;

/// <summary>
/// Orquestador comun reusable por TODOS los consumers de eventos del pedido. Encapsula la matriz
/// de idempotencia en 3 niveles (lookup por clave de negocio, side-effect guard transaccional,
/// dedup por unique index a nivel BD) para que cada consumer solo se preocupe por su mapeo
/// especifico (evento -&gt; destinatario + plantilla).
/// </summary>
public interface INotifyHandler
{
    /// <summary>
    /// Ejecuta el flujo completo de envio idempotente. Siempre devuelve
    /// <see cref="Result.Success()"/> excepto si la validacion del aggregate falla
    /// (<see cref="Domain.Notifications.NotificationErrors.MissingRecipientEmail"/> o
    /// <see cref="Domain.Notifications.NotificationErrors.MissingSubject"/>), en cuyo caso el caller
    /// loguea y sale (el evento ya fue procesado, no se reintenta por un dato defectuoso).
    /// </summary>
    Task<Result> SendAsync(NotifyRequest request, CancellationToken cancellationToken);
}
