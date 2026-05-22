using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Catalog.Domain.Items;

/// <summary>
/// Reserva (hold) de stock tomada por un pedido durante su saga. Es el ancla de idempotencia del
/// patron Reserve -> Commit/Release: existe a lo sumo una por <c>(OrderId, ItemId)</c>. El hold
/// aparta unidades sin descontarlas del stock fisico; al confirmarse se descuentan, al liberarse o
/// expirar (por <see cref="ExpiresAtUtc"/>) vuelven al disponible. Agregado independiente del
/// <see cref="StockLevel"/> al que afecta (que mantiene el contador agregado de reservas).
/// </summary>
public sealed class StockReservation : AggregateRoot<Guid>
{
    private StockReservation()
    {
    }

    private StockReservation(Guid id, Guid orderId, ItemId itemId, Guid merchantId, int quantity, DateTime expiresAtUtc, DateTime utcNow)
        : base(id)
    {
        OrderId = orderId;
        ItemId = itemId;
        MerchantId = merchantId;
        Quantity = quantity;
        Status = StockReservationStatus.Held;
        CreatedAtUtc = utcNow;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary>Pedido dueno del hold (clave de idempotencia junto con <see cref="ItemId"/>).</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Item reservado.</summary>
    public ItemId ItemId { get; private set; }

    /// <summary>Merchant dueno del item.</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>Unidades reservadas.</summary>
    public int Quantity { get; private set; }

    /// <summary>Estado del hold.</summary>
    public StockReservationStatus Status { get; private set; }

    /// <summary>Momento en que se tomo el hold (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Vencimiento del hold (UTC): pasado este punto un hold <see cref="StockReservationStatus.Held"/> puede liberarse por TTL.</summary>
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>Ultima actualizacion (UTC).</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Indica si el hold sigue activo (no confirmado ni liberado).</summary>
    public bool IsHeld => Status == StockReservationStatus.Held;

    /// <summary>Crea un hold para un pedido sobre un item.</summary>
    public static StockReservation Create(Guid orderId, ItemId itemId, Guid merchantId, int quantity, DateTime expiresAtUtc, DateTime utcNow) =>
        new(Guid.CreateVersion7(), orderId, itemId, merchantId, quantity, expiresAtUtc, utcNow);

    /// <summary>Indica si el hold esta vencido a la fecha dada (solo relevante mientras siga <see cref="StockReservationStatus.Held"/>).</summary>
    public bool IsExpired(DateTime utcNow) => Status == StockReservationStatus.Held && utcNow >= ExpiresAtUtc;

    /// <summary>Marca el hold como confirmado (las unidades ya se descontaron del stock fisico). Idempotente.</summary>
    public void Commit(DateTime utcNow)
    {
        if (Status != StockReservationStatus.Held)
        {
            return;
        }

        Status = StockReservationStatus.Committed;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Marca el hold como liberado (las unidades vuelven al disponible). Idempotente.</summary>
    public void Release(DateTime utcNow)
    {
        if (Status != StockReservationStatus.Held)
        {
            return;
        }

        Status = StockReservationStatus.Released;
        UpdatedAtUtc = utcNow;
    }
}
