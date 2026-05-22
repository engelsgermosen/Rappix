namespace Rappix.Catalog.Api.Contracts;

/// <summary>Cuerpo para crear una categoria.</summary>
public sealed record CreateCategoryRequest(string Name, int SortOrder);

/// <summary>Cuerpo para crear un item. Si TracksInventory, InitialStock fija el stock inicial.</summary>
public sealed record CreateItemRequest(
    Guid? CategoryId,
    string Name,
    string? Description,
    decimal PriceAmount,
    string? Currency,
    bool TracksInventory,
    int InitialStock,
    IReadOnlyDictionary<string, string>? Attributes);

/// <summary>Cuerpo para actualizar los datos de un item.</summary>
public sealed record UpdateItemRequest(
    Guid? CategoryId,
    string Name,
    string? Description,
    decimal PriceAmount,
    string? Currency);

/// <summary>Cuerpo para reemplazar los atributos por vertical de un item.</summary>
public sealed record SetItemAttributesRequest(IReadOnlyDictionary<string, string> Attributes);

/// <summary>Cuerpo para publicar/ocultar un item.</summary>
public sealed record SetAvailabilityRequest(bool Available);

/// <summary>Cuerpo para agregar un grupo de modificadores.</summary>
public sealed record AddModifierRequest(string Name, bool IsRequired, int MinSelections, int MaxSelections);

/// <summary>Cuerpo para agregar una opcion a un grupo de modificadores.</summary>
public sealed record AddModifierOptionRequest(string Name, decimal PriceDelta);

/// <summary>Cuerpo para ajustar el stock. Mode admite "Set" (valor absoluto) o "Restock" (suma).</summary>
public sealed record AdjustStockRequest(string Mode, int Quantity);
