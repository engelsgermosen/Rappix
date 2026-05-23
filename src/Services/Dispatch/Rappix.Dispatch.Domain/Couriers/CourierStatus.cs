namespace Rappix.Dispatch.Domain.Couriers;

/// <summary>Estado de disponibilidad del courier.</summary>
public enum CourierStatus
{
    /// <summary>Desconectado: no recibe asignaciones, no aparece en Redis Geo.</summary>
    Offline = 0,

    /// <summary>Conectado y disponible: en Redis Geo, candidato para asignacion.</summary>
    Online = 1,

    /// <summary>Asignado a un pedido: fuera de Redis Geo hasta liberar.</summary>
    Busy = 2,
}
