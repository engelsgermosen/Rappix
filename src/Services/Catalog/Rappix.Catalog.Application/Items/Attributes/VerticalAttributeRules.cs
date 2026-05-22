using System.Globalization;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Domain.Catalogs;

namespace Rappix.Catalog.Application.Items.Attributes;

/// <summary>
/// Reglas de atributos por vertical (función pura, fácil de testear). Cada vertical declara sus
/// claves permitidas, las obligatorias y el tipo esperado (booleano/numerico) de algunas claves.
/// Las claves se comparan sin distinguir mayusculas. Validar permite atributos ausentes salvo los
/// obligatorios; rechaza claves desconocidas y valores con el tipo incorrecto.
/// </summary>
public static class VerticalAttributeRules
{
    private sealed record Rule(
        IReadOnlySet<string> Allowed,
        IReadOnlySet<string> Required,
        IReadOnlySet<string> Boolean,
        IReadOnlySet<string> Numeric);

    private static readonly Dictionary<VerticalType, Rule> Rules =
        new()
        {
            [VerticalType.Food] = new(
                Allowed: Set("spicyLevel", "calories", "isVegetarian", "allergens", "portionSize"),
                Required: Set(),
                Boolean: Set("isVegetarian"),
                Numeric: Set("calories", "spicyLevel")),
            [VerticalType.Pharmacy] = new(
                Allowed: Set("requiresPrescription", "activeIngredient", "dosage", "laboratory"),
                Required: Set("requiresPrescription"),
                Boolean: Set("requiresPrescription"),
                Numeric: Set()),
            [VerticalType.Grocery] = new(
                Allowed: Set("brand", "unit", "barcode", "weightGrams"),
                Required: Set(),
                Boolean: Set(),
                Numeric: Set("weightGrams")),
            [VerticalType.Parcel] = new(
                Allowed: Set("maxWeightKg", "dimensions", "fragile"),
                Required: Set(),
                Boolean: Set("fragile"),
                Numeric: Set("maxWeightKg")),
        };

    /// <summary>Valida los atributos contra las reglas del vertical. Devuelve el primer error encontrado.</summary>
    public static Result Validate(VerticalType vertical, IReadOnlyDictionary<string, string> attributes)
    {
        if (!Rules.TryGetValue(vertical, out Rule? rule))
        {
            return Result.Success();
        }

        foreach (KeyValuePair<string, string> attribute in attributes)
        {
            if (!rule.Allowed.Contains(attribute.Key))
            {
                return Result.Failure(AttributeErrors.UnknownKey(attribute.Key, vertical.ToString()));
            }

            if (rule.Boolean.Contains(attribute.Key) && !bool.TryParse(attribute.Value, out _))
            {
                return Result.Failure(AttributeErrors.InvalidBoolean(attribute.Key));
            }

            if (rule.Numeric.Contains(attribute.Key)
                && !decimal.TryParse(attribute.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            {
                return Result.Failure(AttributeErrors.InvalidNumber(attribute.Key));
            }
        }

        foreach (string required in rule.Required)
        {
            if (!attributes.TryGetValue(required, out string? value) || string.IsNullOrWhiteSpace(value))
            {
                return Result.Failure(AttributeErrors.MissingRequired(required, vertical.ToString()));
            }
        }

        return Result.Success();
    }

    private static HashSet<string> Set(params string[] values) => new(values, StringComparer.OrdinalIgnoreCase);
}
