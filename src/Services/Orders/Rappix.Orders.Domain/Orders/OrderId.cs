namespace Rappix.Orders.Domain.Orders;

/// <summary>Identificador fuertemente tipado de un pedido (UUIDv7). Es la clave de correlacion de la saga.</summary>
public readonly record struct OrderId(Guid Value)
{
    /// <summary>Genera un nuevo identificador basado en UUIDv7.</summary>
    public static OrderId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
