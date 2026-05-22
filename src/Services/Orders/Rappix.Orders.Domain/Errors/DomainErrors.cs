using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Orders.Domain.Errors;

/// <summary>Errores del agregado pedido (Order) y sus lineas.</summary>
public static class OrderErrors
{
    /// <summary>Pedido no encontrado.</summary>
    public static readonly Error NotFound =
        Error.NotFound("Orders.Order.NotFound", "Pedido no encontrado.");

    /// <summary>El pedido no tiene lineas.</summary>
    public static readonly Error NoLines =
        Error.Validation("Orders.Order.NoLines", "El pedido debe incluir al menos una linea.");

    /// <summary>El pedido no pertenece al cliente indicado.</summary>
    public static readonly Error NotOwnedByCustomer =
        Error.Forbidden("Orders.Order.NotOwnedByCustomer", "El pedido no pertenece a este cliente.");

    /// <summary>El pedido no pertenece al merchant indicado.</summary>
    public static readonly Error NotForMerchant =
        Error.Forbidden("Orders.Order.NotForMerchant", "El pedido no pertenece a este merchant.");

    /// <summary>La cotizacion no es valida para crear el pedido (expirada, ya consumida o de otro cliente/merchant).</summary>
    public static readonly Error QuoteNotUsable =
        Error.Conflict("Orders.Order.QuoteNotUsable", "La cotizacion no es valida: expiro, ya fue usada o no corresponde.");

    /// <summary>El pedido no esta en un estado en el que esta accion sea valida.</summary>
    public static readonly Error InvalidState =
        Error.Conflict("Orders.Order.InvalidState", "La operacion no es valida para el estado actual del pedido.");

    /// <summary>El pedido ya no puede cancelarse (paso el punto de no retorno o ya es terminal).</summary>
    public static readonly Error NotCancellable =
        Error.Conflict("Orders.Order.NotCancellable", "El pedido ya no puede cancelarse en su estado actual.");
}

/// <summary>Errores del value object direccion de entrega.</summary>
public static class DeliveryAddressErrors
{
    /// <summary>Calle/linea de direccion obligatoria.</summary>
    public static readonly Error StreetRequired =
        Error.Validation("Orders.DeliveryAddress.StreetRequired", "La direccion de entrega es obligatoria.");

    /// <summary>Coordenadas fuera de rango.</summary>
    public static readonly Error InvalidCoordinates =
        Error.Validation("Orders.DeliveryAddress.InvalidCoordinates", "Las coordenadas de entrega son invalidas.");
}

/// <summary>Errores de las lineas del pedido.</summary>
public static class OrderLineErrors
{
    /// <summary>Cantidad invalida (debe ser positiva).</summary>
    public static readonly Error InvalidQuantity =
        Error.Validation("Orders.OrderLine.InvalidQuantity", "La cantidad de cada linea debe ser positiva.");

    /// <summary>Precio unitario negativo.</summary>
    public static readonly Error NegativeUnitPrice =
        Error.Validation("Orders.OrderLine.NegativeUnitPrice", "El precio unitario no puede ser negativo.");
}
