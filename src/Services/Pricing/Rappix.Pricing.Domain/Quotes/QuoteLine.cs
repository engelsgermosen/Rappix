using Rappix.BuildingBlocks.Core.Domain;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Domain.Errors;

namespace Rappix.Pricing.Domain.Quotes;

/// <summary>
/// Linea de una cotizacion: un item del carrito con su precio unitario (resuelto desde Catalog), el
/// total de modificadores seleccionados por unidad y la cantidad. El subtotal de linea es
/// (precio + modificadores) x cantidad. Entidad hija del agregado <see cref="Quote"/>.
/// </summary>
public sealed class QuoteLine : Entity<Guid>
{
    private QuoteLine()
    {
    }

    private QuoteLine(Guid id, Guid itemId, string itemName, decimal unitPrice, decimal modifierTotal, int quantity)
        : base(id)
    {
        ItemId = itemId;
        ItemName = itemName;
        UnitPrice = unitPrice;
        ModifierTotal = modifierTotal;
        Quantity = quantity;
        LineSubtotal = (unitPrice + modifierTotal) * quantity;
    }

    /// <summary>Cotizacion a la que pertenece (clave foranea).</summary>
    public QuoteId QuoteId { get; private set; }

    /// <summary>Item cotizado.</summary>
    public Guid ItemId { get; private set; }

    /// <summary>Nombre del item al momento de cotizar (snapshot).</summary>
    public string ItemName { get; private set; } = null!;

    /// <summary>Precio unitario del item (resuelto desde Catalog).</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Suma de los modificadores seleccionados por unidad.</summary>
    public decimal ModifierTotal { get; private set; }

    /// <summary>Cantidad pedida.</summary>
    public int Quantity { get; private set; }

    /// <summary>Subtotal de la linea = (precio unitario + modificadores) x cantidad.</summary>
    public decimal LineSubtotal { get; private set; }

    /// <summary>Crea una linea validando cantidad positiva y montos no negativos.</summary>
    public static Result<QuoteLine> Create(Guid itemId, string itemName, decimal unitPrice, decimal modifierTotal, int quantity)
    {
        if (quantity <= 0)
        {
            return Result.Failure<QuoteLine>(QuoteLineErrors.InvalidQuantity);
        }

        if (unitPrice < 0m)
        {
            return Result.Failure<QuoteLine>(QuoteLineErrors.NegativeUnitPrice);
        }

        if (modifierTotal < 0m)
        {
            return Result.Failure<QuoteLine>(QuoteLineErrors.NegativeModifierTotal);
        }

        return new QuoteLine(Guid.CreateVersion7(), itemId, itemName, unitPrice, modifierTotal, quantity);
    }
}
