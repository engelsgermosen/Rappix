using Rappix.BuildingBlocks.Core.Domain;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Domain.Abstractions;
using Rappix.Catalog.Domain.Items.Events;

namespace Rappix.Catalog.Domain.Items;

/// <summary>
/// Agregado raiz de un item del catalogo. Pertenece a un merchant y opcionalmente a una categoria.
/// Guarda atributos por vertical (JSONB; la validacion semantica vive en la capa de aplicacion),
/// la key de su foto en MinIO y sus grupos de modificadores. El stock vive en el agregado
/// <see cref="StockLevel"/> (separado para aislar la concurrencia de los decrementos).
/// </summary>
public sealed class Item : AggregateRoot<ItemId>, IHasDomainEvents
{
    /// <summary>Largo maximo del nombre.</summary>
    public const int MaxNameLength = 120;

    /// <summary>Largo maximo de la descripcion.</summary>
    public const int MaxDescriptionLength = 2000;

    private readonly List<Modifier> _modifiers = [];

    // No es readonly: EF Core reasigna este campo al materializar el atributo JSONB.
    private Dictionary<string, string> _attributes = new(StringComparer.OrdinalIgnoreCase);

    private Item()
    {
    }

    private Item(
        ItemId id,
        Guid merchantId,
        Guid? categoryId,
        string name,
        string? description,
        Money price,
        bool tracksInventory,
        DateTime utcNow)
        : base(id)
    {
        MerchantId = merchantId;
        CategoryId = categoryId;
        Name = name;
        Description = description;
        Price = price;
        TracksInventory = tracksInventory;
        IsAvailable = true;
        IsDeleted = false;
        CreatedAtUtc = utcNow;
    }

    /// <summary>Merchant dueno del item.</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>Categoria a la que pertenece (opcional; debe ser del catalogo del merchant).</summary>
    public Guid? CategoryId { get; private set; }

    /// <summary>Nombre del item.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Descripcion (opcional).</summary>
    public string? Description { get; private set; }

    /// <summary>Precio base. La logica de pricing dinamico vive en el servicio Pricing.</summary>
    public Money Price { get; private set; } = null!;

    /// <summary>Key de la foto en MinIO (bucket catalog-items), si tiene.</summary>
    public string? PhotoObjectKey { get; private set; }

    /// <summary>Disponibilidad publicada por el merchant (independiente del stock).</summary>
    public bool IsAvailable { get; private set; }

    /// <summary>Indica si el item lleva control de inventario (tiene un <see cref="StockLevel"/> asociado).</summary>
    public bool TracksInventory { get; private set; }

    /// <summary>Borrado logico.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Momento de creacion (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Momento de la ultima actualizacion (UTC).</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Atributos por vertical (JSONB). Clave/valor en texto; la validacion por vertical es de la capa de aplicacion.</summary>
    public IReadOnlyDictionary<string, string> Attributes => _attributes;

    /// <summary>Grupos de modificadores.</summary>
    public IReadOnlyCollection<Modifier> Modifiers => _modifiers.AsReadOnly();

    /// <summary>Crea un item. Eleva <see cref="ItemCreatedDomainEvent"/>.</summary>
    public static Result<Item> Create(
        Guid merchantId,
        Guid? categoryId,
        string name,
        string? description,
        Money price,
        bool tracksInventory,
        IReadOnlyDictionary<string, string>? attributes,
        DateTime utcNow)
    {
        Result<string> normalizedName = NormalizeName(name);
        if (normalizedName.IsFailure)
        {
            return Result.Failure<Item>(normalizedName.Error);
        }

        Result<string?> normalizedDescription = NormalizeDescription(description);
        if (normalizedDescription.IsFailure)
        {
            return Result.Failure<Item>(normalizedDescription.Error);
        }

        Item item = new(ItemId.New(), merchantId, categoryId, normalizedName.Value, normalizedDescription.Value, price, tracksInventory, utcNow);
        if (attributes is not null)
        {
            item.ReplaceAttributesInternal(attributes);
        }

        item.RaiseDomainEvent(new ItemCreatedDomainEvent(merchantId, item.Id, item.Name, price.Amount, price.Currency));
        return item;
    }

    /// <summary>Actualiza nombre, descripcion, categoria y precio.</summary>
    public Result UpdateDetails(Guid? categoryId, string name, string? description, Money price, DateTime utcNow)
    {
        Result mutable = EnsureNotDeleted();
        if (mutable.IsFailure)
        {
            return mutable;
        }

        Result<string> normalizedName = NormalizeName(name);
        if (normalizedName.IsFailure)
        {
            return Result.Failure(normalizedName.Error);
        }

        Result<string?> normalizedDescription = NormalizeDescription(description);
        if (normalizedDescription.IsFailure)
        {
            return Result.Failure(normalizedDescription.Error);
        }

        CategoryId = categoryId;
        Name = normalizedName.Value;
        Description = normalizedDescription.Value;
        Price = price;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Reemplaza todos los atributos por vertical.</summary>
    public Result ReplaceAttributes(IReadOnlyDictionary<string, string> attributes, DateTime utcNow)
    {
        Result mutable = EnsureNotDeleted();
        if (mutable.IsFailure)
        {
            return mutable;
        }

        ReplaceAttributesInternal(attributes);
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Asigna la key de la foto (la validacion de imagen ocurre antes, en la capa de aplicacion).</summary>
    public void SetPhoto(string objectKey, DateTime utcNow)
    {
        PhotoObjectKey = objectKey;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Quita la foto.</summary>
    public void RemovePhoto(DateTime utcNow)
    {
        PhotoObjectKey = null;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Publica el item (visible a clientes).</summary>
    public Result MakeAvailable(DateTime utcNow)
    {
        Result mutable = EnsureNotDeleted();
        if (mutable.IsFailure)
        {
            return mutable;
        }

        IsAvailable = true;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Oculta el item (no visible a clientes).</summary>
    public Result MakeUnavailable(DateTime utcNow)
    {
        Result mutable = EnsureNotDeleted();
        if (mutable.IsFailure)
        {
            return mutable;
        }

        IsAvailable = false;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Agrega un grupo de modificadores.</summary>
    public Result<Modifier> AddModifier(string name, bool isRequired, int minSelections, int maxSelections, DateTime utcNow)
    {
        Result mutable = EnsureNotDeleted();
        if (mutable.IsFailure)
        {
            return Result.Failure<Modifier>(mutable.Error);
        }

        Result<Modifier> modifier = Modifier.Create(Id, name, isRequired, minSelections, maxSelections);
        if (modifier.IsFailure)
        {
            return modifier;
        }

        _modifiers.Add(modifier.Value);
        UpdatedAtUtc = utcNow;
        return modifier;
    }

    /// <summary>Agrega una opcion a un grupo de modificadores existente.</summary>
    public Result AddModifierOption(Guid modifierId, string name, decimal priceDelta, DateTime utcNow)
    {
        Result mutable = EnsureNotDeleted();
        if (mutable.IsFailure)
        {
            return mutable;
        }

        Modifier? modifier = _modifiers.Find(item => item.Id == modifierId);
        if (modifier is null)
        {
            return Result.Failure(ModifierErrors.NotFound);
        }

        Result added = modifier.AddOption(name, priceDelta);
        if (added.IsFailure)
        {
            return added;
        }

        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Quita un grupo de modificadores.</summary>
    public Result RemoveModifier(Guid modifierId, DateTime utcNow)
    {
        Modifier? modifier = _modifiers.Find(item => item.Id == modifierId);
        if (modifier is null)
        {
            return Result.Failure(ModifierErrors.NotFound);
        }

        _modifiers.Remove(modifier);
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Borrado logico. Tambien lo oculta.</summary>
    public void Delete(DateTime utcNow)
    {
        IsDeleted = true;
        IsAvailable = false;
        UpdatedAtUtc = utcNow;
    }

    private void ReplaceAttributesInternal(IReadOnlyDictionary<string, string> attributes)
    {
        _attributes.Clear();
        foreach (KeyValuePair<string, string> attribute in attributes)
        {
            _attributes[attribute.Key] = attribute.Value;
        }
    }

    private Result EnsureNotDeleted() =>
        IsDeleted ? Result.Failure(ItemErrors.Deleted) : Result.Success();

    private static Result<string> NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<string>(ItemErrors.NameRequired);
        }

        string trimmed = name.Trim();
        return trimmed.Length > MaxNameLength
            ? Result.Failure<string>(ItemErrors.NameTooLong)
            : trimmed;
    }

    private static Result<string?> NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return Result.Success<string?>(null);
        }

        string trimmed = description.Trim();
        return trimmed.Length > MaxDescriptionLength
            ? Result.Failure<string?>(ItemErrors.DescriptionTooLong)
            : Result.Success<string?>(trimmed);
    }
}
