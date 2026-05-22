using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Catalog.Domain;

/// <summary>Errores del agregado catalogo y sus categorias.</summary>
public static class CatalogErrors
{
    /// <summary>Catalogo no encontrado.</summary>
    public static readonly Error NotFound =
        Error.NotFound("Catalog.Catalog.NotFound", "Catalogo no encontrado.");

    /// <summary>Ya existe un catalogo para el merchant.</summary>
    public static readonly Error AlreadyExistsForMerchant =
        Error.Conflict("Catalog.Catalog.AlreadyExistsForMerchant", "El merchant ya tiene un catalogo.");

    /// <summary>El catalogo esta deshabilitado (merchant inactivo/suspendido).</summary>
    public static readonly Error Disabled =
        Error.Conflict("Catalog.Catalog.Disabled", "El catalogo esta deshabilitado porque el merchant no esta activo.");

    /// <summary>Categoria no encontrada.</summary>
    public static readonly Error CategoryNotFound =
        Error.NotFound("Catalog.Category.NotFound", "Categoria no encontrada.");

    /// <summary>Nombre de categoria obligatorio.</summary>
    public static readonly Error CategoryNameRequired =
        Error.Validation("Catalog.Category.NameRequired", "El nombre de la categoria es obligatorio.");

    /// <summary>Nombre de categoria demasiado largo.</summary>
    public static readonly Error CategoryNameTooLong =
        Error.Validation("Catalog.Category.NameTooLong", "El nombre de la categoria es demasiado largo.");

    /// <summary>Nombre de categoria duplicado en el catalogo.</summary>
    public static Error DuplicateCategoryName(string name) =>
        Error.Conflict("Catalog.Category.Duplicate", $"Ya existe una categoria llamada '{name}'.");
}

/// <summary>Errores del agregado item.</summary>
public static class ItemErrors
{
    /// <summary>Item no encontrado.</summary>
    public static readonly Error NotFound =
        Error.NotFound("Catalog.Item.NotFound", "Item no encontrado.");

    /// <summary>El item fue borrado.</summary>
    public static readonly Error Deleted =
        Error.Conflict("Catalog.Item.Deleted", "El item fue eliminado.");

    /// <summary>Nombre de item obligatorio.</summary>
    public static readonly Error NameRequired =
        Error.Validation("Catalog.Item.NameRequired", "El nombre del item es obligatorio.");

    /// <summary>Nombre de item demasiado largo.</summary>
    public static readonly Error NameTooLong =
        Error.Validation("Catalog.Item.NameTooLong", "El nombre del item es demasiado largo.");

    /// <summary>Descripcion de item demasiado larga.</summary>
    public static readonly Error DescriptionTooLong =
        Error.Validation("Catalog.Item.DescriptionTooLong", "La descripcion del item es demasiado larga.");

    /// <summary>El item no pertenece al merchant indicado.</summary>
    public static readonly Error NotOwnedByMerchant =
        Error.Forbidden("Catalog.Item.NotOwned", "El item no pertenece a este merchant.");

    /// <summary>La categoria no pertenece al catalogo del merchant.</summary>
    public static readonly Error CategoryNotInCatalog =
        Error.Validation("Catalog.Item.CategoryNotInCatalog", "La categoria no pertenece al catalogo del merchant.");

    /// <summary>El item no lleva control de inventario.</summary>
    public static readonly Error NotInventoryTracked =
        Error.Conflict("Catalog.Item.NotInventoryTracked", "El item no lleva control de inventario.");

    /// <summary>El item esta agotado o no disponible.</summary>
    public static readonly Error NotPurchasable =
        Error.Conflict("Catalog.Item.NotPurchasable", "El item no esta disponible para la compra.");
}

/// <summary>Errores del value object Money.</summary>
public static class MoneyErrors
{
    /// <summary>Monto negativo.</summary>
    public static readonly Error NegativeAmount =
        Error.Validation("Catalog.Money.NegativeAmount", "El monto no puede ser negativo.");

    /// <summary>Moneda invalida.</summary>
    public static readonly Error InvalidCurrency =
        Error.Validation("Catalog.Money.InvalidCurrency", "La moneda debe ser un codigo ISO 4217 de 3 letras.");
}

/// <summary>Errores del agregado de stock.</summary>
public static class StockErrors
{
    /// <summary>Cantidad inicial/absoluta negativa.</summary>
    public static readonly Error NegativeQuantity =
        Error.Validation("Catalog.Stock.NegativeQuantity", "La cantidad no puede ser negativa.");

    /// <summary>Cantidad de movimiento invalida (debe ser positiva).</summary>
    public static readonly Error InvalidQuantity =
        Error.Validation("Catalog.Stock.InvalidQuantity", "La cantidad debe ser positiva.");

    /// <summary>Stock insuficiente para el decremento.</summary>
    public static readonly Error InsufficientStock =
        Error.Conflict("Catalog.Stock.Insufficient", "No hay stock suficiente.");

    /// <summary>No existe nivel de stock para el item.</summary>
    public static readonly Error NotFound =
        Error.NotFound("Catalog.Stock.NotFound", "No existe control de inventario para el item.");
}

/// <summary>Errores de los grupos de modificadores y sus opciones.</summary>
public static class ModifierErrors
{
    /// <summary>Grupo de modificadores no encontrado.</summary>
    public static readonly Error NotFound =
        Error.NotFound("Catalog.Modifier.NotFound", "Grupo de modificadores no encontrado.");

    /// <summary>Nombre de grupo obligatorio.</summary>
    public static readonly Error NameRequired =
        Error.Validation("Catalog.Modifier.NameRequired", "El nombre del grupo de modificadores es obligatorio.");

    /// <summary>Nombre de grupo demasiado largo.</summary>
    public static readonly Error NameTooLong =
        Error.Validation("Catalog.Modifier.NameTooLong", "El nombre del grupo de modificadores es demasiado largo.");

    /// <summary>Rango de seleccion invalido.</summary>
    public static readonly Error InvalidSelectionRange =
        Error.Validation("Catalog.Modifier.InvalidSelectionRange", "El rango de seleccion del grupo es invalido.");

    /// <summary>Nombre de opcion obligatorio.</summary>
    public static readonly Error OptionNameRequired =
        Error.Validation("Catalog.Modifier.OptionNameRequired", "El nombre de la opcion es obligatorio.");

    /// <summary>Opcion duplicada en el grupo.</summary>
    public static Error DuplicateOption(string name) =>
        Error.Conflict("Catalog.Modifier.DuplicateOption", $"Ya existe una opcion llamada '{name}'.");
}
