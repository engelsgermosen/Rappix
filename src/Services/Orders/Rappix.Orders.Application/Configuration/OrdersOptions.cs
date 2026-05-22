namespace Rappix.Orders.Application.Configuration;

/// <summary>Opciones del servicio Orders: timeouts de la saga, TTL de reserva y simulacion de pago/courier.</summary>
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

    /// <summary>Habilita los responders simulados de pago/courier/entrega (se apagan cuando lleguen los servicios reales).</summary>
    public bool EnableSimulatedResponders { get; set; } = true;

    /// <summary>Configuracion de los responders simulados.</summary>
    public SimulationOptions Simulation { get; set; } = new();

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

    /// <summary>Resultados simulados configurables para demostrar caminos felices y de fallo sin Payments/Dispatch reales.</summary>
    public sealed class SimulationOptions
    {
        /// <summary>Resultado del pago simulado: Success | Fail | Timeout.</summary>
        public string PaymentOutcome { get; set; } = "Success";

        /// <summary>Resultado del courier simulado: Success | Unavailable | Timeout.</summary>
        public string CourierOutcome { get; set; } = "Success";

        /// <summary>Latencia simulada del pago (ms).</summary>
        public int PaymentDelayMs { get; set; }

        /// <summary>Latencia simulada del courier (ms).</summary>
        public int CourierDelayMs { get; set; }

        /// <summary>Si el responder de entrega completa el pedido automaticamente (lleva InProgress -> Completed).</summary>
        public bool AutoDeliver { get; set; } = true;

        /// <summary>Retraso simulado de la entrega (ms).</summary>
        public int DeliveryDelayMs { get; set; }
    }
}
