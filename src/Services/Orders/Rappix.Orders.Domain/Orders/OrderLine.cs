using Rappix.BuildingBlocks.Core.Domain;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Orders.Domain.Errors;

namespace Rappix.Orders.Domain.Orders;

/// <summary>
/// Linea del pedido: snapshot INMUTABLE de un item al momento de confirmar (itemId, nombre, precio
/// unitario, modificadores y cantidad copiados del quote congelado). NO es una referencia viva a Catalog:
/// si el merchant cambia el precio luego, el pedido conserva el precio del quote. Entidad hija de Order.
/// </summary>
public sealed class OrderLine : Entity<Guid>
{
    private OrderLine()
    {
    }

    private OrderLine(Guid id, Guid itemId, string itemName, decimal unitPrice, decimal modifierTotal, int quantity)
        : base(id)
    {
        ItemId = itemId;
        ItemName = itemName;
        UnitPrice = unitPrice;
        ModifierTotal = modifierTotal;
        Quantity = quantity;
        LineSubtotal = (unitPrice + modifierTotal) * quantity;
    }

    /// <summary>Pedido al que pertenece (clave foranea).</summary>
    public OrderId OrderId { get; private set; }

    /// <summary>Item pedido (referencia a Catalog, solo como identificador del snapshot).</summary>
    public Guid ItemId { get; private set; }

    /// <summary>Nombre del item al momento del pedido (snapshot).</summary>
    public string ItemName { get; private set; } = null!;

    /// <summary>Precio unitario congelado del quote.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Total de modificadores por unidad (snapshot).</summary>
    public decimal ModifierTotal { get; private set; }

    /// <summary>Cantidad pedida.</summary>
    public int Quantity { get; private set; }

    /// <summary>Subtotal de la linea = (precio unitario + modificadores) x cantidad.</summary>
    public decimal LineSubtotal { get; private set; }

    /// <summary>Crea una linea snapshot validando cantidad positiva y precio no negativo.</summary>
    public static Result<OrderLine> Create(Guid itemId, string itemName, decimal unitPrice, decimal modifierTotal, int quantity)
    {
        if (quantity <= 0)
        {
            return Result.Failure<OrderLine>(OrderLineErrors.InvalidQuantity);
        }

        if (unitPrice < 0m || modifierTotal < 0m)
        {
            return Result.Failure<OrderLine>(OrderLineErrors.NegativeUnitPrice);
        }

        return new OrderLine(Guid.CreateVersion7(), itemId, itemName, unitPrice, modifierTotal, quantity);
    }
}
