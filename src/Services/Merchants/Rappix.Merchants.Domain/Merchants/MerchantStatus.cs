namespace Rappix.Merchants.Domain.Merchants;

/// <summary>Estados del ciclo de vida de un merchant (maquina de estados en el agregado).</summary>
public enum MerchantStatus
{
    /// <summary>Recien creado por el consumer; el owner completa la informacion.</summary>
    Draft = 0,

    /// <summary>Enviado a revision por el owner.</summary>
    Pending = 1,

    /// <summary>Aprobado por admin; visible a clientes.</summary>
    Active = 2,

    /// <summary>Pausado temporalmente (owner o admin).</summary>
    Paused = 3,

    /// <summary>Suspendido por admin (sancion).</summary>
    Suspended = 4,

    /// <summary>Rechazada la aprobacion por admin.</summary>
    Rejected = 5,
}
