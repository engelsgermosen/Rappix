using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Catalog.Application.Items.Attributes;

/// <summary>Errores de validacion de atributos por vertical.</summary>
public static class AttributeErrors
{
    /// <summary>Una clave de atributo no esta permitida para el vertical.</summary>
    public static Error UnknownKey(string key, string vertical) =>
        Error.Validation("Catalog.Attributes.UnknownKey", $"El atributo '{key}' no aplica al vertical {vertical}.");

    /// <summary>Falta un atributo requerido para el vertical.</summary>
    public static Error MissingRequired(string key, string vertical) =>
        Error.Validation("Catalog.Attributes.MissingRequired", $"El atributo '{key}' es obligatorio para el vertical {vertical}.");

    /// <summary>El valor de un atributo booleano no es valido.</summary>
    public static Error InvalidBoolean(string key) =>
        Error.Validation("Catalog.Attributes.InvalidBoolean", $"El atributo '{key}' debe ser 'true' o 'false'.");

    /// <summary>El valor de un atributo numerico no es valido.</summary>
    public static Error InvalidNumber(string key) =>
        Error.Validation("Catalog.Attributes.InvalidNumber", $"El atributo '{key}' debe ser numerico.");
}
