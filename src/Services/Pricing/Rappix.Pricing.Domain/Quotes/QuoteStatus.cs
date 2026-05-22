namespace Rappix.Pricing.Domain.Quotes;

/// <summary>Estado del ciclo de vida de una cotizacion.</summary>
public enum QuoteStatus
{
    /// <summary>Vigente: dentro de su ventana de expiracion y aun no consumida.</summary>
    Active = 0,

    /// <summary>Consumida: un pedido la referencio (estado terminal).</summary>
    Consumed = 1,

    /// <summary>Expirada: venció su ventana sin consumirse (estado terminal).</summary>
    Expired = 2,
}
