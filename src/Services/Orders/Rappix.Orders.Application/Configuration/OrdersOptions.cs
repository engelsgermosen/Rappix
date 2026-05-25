namespace Rappix.Orders.Application.Configuration;

/// <summary>Opciones del servicio Orders: timeouts de la saga y TTL de reserva de stock.</summary>
public sealed class OrdersOptions
{
    /// <summary>Nombre de la seccion en appsettings.</summary>
    public const string SectionName = "Orders";

    /// <summary>Timeouts de cada espera de la saga (configurables para bajarlos en tests/demo).</summary>
    public TimeoutOptions Timeouts { get; set; } = new();

    /// <summary>
    /// TTL del hold de stock en segundos. Debe superar la duracion maxima de la saga (suma de timeouts)
    /// para que un pedido en curso nunca pierda su reserva. Default 1800s (30 min) frente a ~10 min de esperas.
    /// </summary>
    public int ReservationTtlSeconds { get; set; } = 1800;

    /// <summary>
    /// Timeouts de las esperas de la saga. Tipo <see cref="TimeSpan"/> para que el binder de configuracion
    /// acepte el formato "hh:mm:ss" (p. ej. Orders__Timeouts__Merchant=00:00:15 baja el timeout a 15s en
    /// demos/tests). Antes eran enteros *Seconds, lo que hacia que un override en formato TimeSpan se ignorara
    /// en silencio y el timeout se quedara en el default.
    /// </summary>
    public sealed class TimeoutOptions
    {
        /// <summary>Espera de aceptacion del merchant. Default 5 min.</summary>
        public TimeSpan Merchant { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>Espera de confirmacion del pago. Default 2 min.</summary>
        public TimeSpan Payment { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>Espera de asignacion de courier. Default 3 min.</summary>
        public TimeSpan Courier { get; set; } = TimeSpan.FromMinutes(3);
    }
}
