using Rappix.BuildingBlocks.Core.Domain;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Domain.Abstractions;
using Rappix.Catalog.Domain.Items.Events;

namespace Rappix.Catalog.Domain.Items;

/// <summary>
/// Nivel de stock de un item con control de inventario. Agregado independiente (PK = ItemId, 1:1
/// con el item) con token de concurrencia optimista (xmin) para evitar sobreventa ante decrementos
/// concurrentes: dos decrementos simultaneos chocan en la fila de stock y uno reintenta.
/// </summary>
public sealed class StockLevel : AggregateRoot<ItemId>, IHasDomainEvents
{
    private StockLevel()
    {
    }

    private StockLevel(ItemId itemId, Guid merchantId, int quantity, DateTime utcNow)
        : base(itemId)
    {
        MerchantId = merchantId;
        Quantity = quantity;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Merchant dueno (para gating y para el evento de agotamiento).</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>Unidades disponibles.</summary>
    public int Quantity { get; private set; }

    /// <summary>Momento de la ultima actualizacion (UTC).</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Indica si el stock esta agotado.</summary>
    public bool IsDepleted => Quantity == 0;

    /// <summary>Crea el nivel de stock inicial de un item.</summary>
    public static Result<StockLevel> Create(ItemId itemId, Guid merchantId, int initialQuantity, DateTime utcNow) =>
        initialQuantity < 0
            ? Result.Failure<StockLevel>(StockErrors.NegativeQuantity)
            : new StockLevel(itemId, merchantId, initialQuantity, utcNow);

    /// <summary>Decrementa el stock. Falla si la cantidad no es positiva o no hay suficiente (previene sobreventa).</summary>
    public Result Decrement(int quantity, DateTime utcNow)
    {
        if (quantity <= 0)
        {
            return Result.Failure(StockErrors.InvalidQuantity);
        }

        if (quantity > Quantity)
        {
            return Result.Failure(StockErrors.InsufficientStock);
        }

        Quantity -= quantity;
        UpdatedAtUtc = utcNow;
        if (Quantity == 0)
        {
            RaiseDomainEvent(new StockDepletedDomainEvent(MerchantId, Id));
        }

        return Result.Success();
    }

    /// <summary>Repone stock.</summary>
    public Result Restock(int quantity, DateTime utcNow)
    {
        if (quantity <= 0)
        {
            return Result.Failure(StockErrors.InvalidQuantity);
        }

        Quantity += quantity;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Fija el stock a un valor absoluto (correccion de inventario).</summary>
    public Result SetQuantity(int quantity, DateTime utcNow)
    {
        if (quantity < 0)
        {
            return Result.Failure(StockErrors.NegativeQuantity);
        }

        bool wasPositive = Quantity > 0;
        Quantity = quantity;
        UpdatedAtUtc = utcNow;
        if (quantity == 0 && wasPositive)
        {
            RaiseDomainEvent(new StockDepletedDomainEvent(MerchantId, Id));
        }

        return Result.Success();
    }
}
