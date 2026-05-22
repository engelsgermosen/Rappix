using Rappix.BuildingBlocks.Core.Domain;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Catalog.Domain.Items;

/// <summary>
/// Grupo de modificadores de un item (p. ej. "Tamano", "Extras") con reglas de seleccion.
/// Entidad hija del agregado <see cref="Item"/>.
/// </summary>
public sealed class Modifier : Entity<Guid>
{
    /// <summary>Largo maximo del nombre del grupo.</summary>
    public const int MaxNameLength = 80;

    private readonly List<ModifierOption> _options = [];

    private Modifier()
    {
    }

    private Modifier(Guid id, ItemId itemId, string name, bool isRequired, int minSelections, int maxSelections)
        : base(id)
    {
        ItemId = itemId;
        Name = name;
        IsRequired = isRequired;
        MinSelections = minSelections;
        MaxSelections = maxSelections;
    }

    /// <summary>Item al que pertenece.</summary>
    public ItemId ItemId { get; private set; }

    /// <summary>Nombre del grupo.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Indica si el cliente debe seleccionar al menos una opcion.</summary>
    public bool IsRequired { get; private set; }

    /// <summary>Minimo de opciones a seleccionar.</summary>
    public int MinSelections { get; private set; }

    /// <summary>Maximo de opciones a seleccionar.</summary>
    public int MaxSelections { get; private set; }

    /// <summary>Opciones disponibles del grupo.</summary>
    public IReadOnlyCollection<ModifierOption> Options => _options.AsReadOnly();

    internal static Result<Modifier> Create(ItemId itemId, string name, bool isRequired, int minSelections, int maxSelections)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<Modifier>(ModifierErrors.NameRequired);
        }

        string trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            return Result.Failure<Modifier>(ModifierErrors.NameTooLong);
        }

        if (minSelections < 0 || maxSelections <= 0 || maxSelections < minSelections)
        {
            return Result.Failure<Modifier>(ModifierErrors.InvalidSelectionRange);
        }

        return new Modifier(Guid.CreateVersion7(), itemId, trimmed, isRequired, minSelections, maxSelections);
    }

    internal Result AddOption(string name, decimal priceDelta)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(ModifierErrors.OptionNameRequired);
        }

        string trimmed = name.Trim();
        if (_options.Exists(option => string.Equals(option.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(ModifierErrors.DuplicateOption(trimmed));
        }

        _options.Add(new ModifierOption(Guid.CreateVersion7(), Id, trimmed, priceDelta));
        return Result.Success();
    }
}
