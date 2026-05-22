using Rappix.BuildingBlocks.Core.Domain;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Domain.Abstractions;

namespace Rappix.Catalog.Domain.Catalogs;

/// <summary>
/// Agregado raiz: el catalogo de un merchant. Hay exactamente uno por merchant. Cachea el
/// <see cref="VerticalType"/> y el estado de habilitacion (gating) que provienen de eventos del
/// servicio Merchants (aprobado/activado/suspendido). Tambien agrupa las categorias.
/// </summary>
public sealed class MerchantCatalog : AggregateRoot<CatalogId>, IHasDomainEvents
{
    /// <summary>Largo maximo del nombre de una categoria.</summary>
    public const int MaxCategoryNameLength = 80;

    private readonly List<Category> _categories = [];

    private MerchantCatalog()
    {
    }

    private MerchantCatalog(CatalogId id, Guid merchantId, VerticalType verticalType, bool isEnabled, DateTime utcNow)
        : base(id)
    {
        MerchantId = merchantId;
        VerticalType = verticalType;
        IsEnabled = isEnabled;
        CreatedAtUtc = utcNow;
    }

    /// <summary>Merchant (servicio Merchants) dueno del catalogo. Unico.</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>Vertical de negocio, cacheado desde el evento de aprobacion.</summary>
    public VerticalType VerticalType { get; private set; }

    /// <summary>Gating: refleja si el merchant esta activo. Se apaga al suspenderlo, se enciende al activarlo.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>Momento de creacion (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Momento de la ultima actualizacion (UTC).</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Categorias del catalogo.</summary>
    public IReadOnlyCollection<Category> Categories => _categories.AsReadOnly();

    /// <summary>Crea el catalogo (lo invoca el consumer de MerchantApproved). Arranca habilitado.</summary>
    public static MerchantCatalog Create(Guid merchantId, VerticalType verticalType, DateTime utcNow) =>
        new(CatalogId.New(), merchantId, verticalType, isEnabled: true, utcNow);

    /// <summary>Habilita el catalogo (merchant reactivado). Idempotente.</summary>
    public void Enable(DateTime utcNow)
    {
        if (IsEnabled)
        {
            return;
        }

        IsEnabled = true;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Deshabilita el catalogo (merchant suspendido). Idempotente.</summary>
    public void Disable(DateTime utcNow)
    {
        if (!IsEnabled)
        {
            return;
        }

        IsEnabled = false;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Agrega una categoria validando nombre (obligatorio, unico, largo).</summary>
    public Result<Category> AddCategory(string name, int sortOrder, DateTime utcNow)
    {
        Result<string> normalized = NormalizeCategoryName(name);
        if (normalized.IsFailure)
        {
            return Result.Failure<Category>(normalized.Error);
        }

        if (_categories.Exists(category => string.Equals(category.Name, normalized.Value, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<Category>(CatalogErrors.DuplicateCategoryName(normalized.Value));
        }

        Category created = new(Guid.CreateVersion7(), Id, normalized.Value, sortOrder);
        _categories.Add(created);
        UpdatedAtUtc = utcNow;
        return created;
    }

    /// <summary>Renombra una categoria existente.</summary>
    public Result RenameCategory(Guid categoryId, string name, DateTime utcNow)
    {
        Category? category = _categories.Find(item => item.Id == categoryId);
        if (category is null)
        {
            return Result.Failure(CatalogErrors.CategoryNotFound);
        }

        Result<string> normalized = NormalizeCategoryName(name);
        if (normalized.IsFailure)
        {
            return Result.Failure(normalized.Error);
        }

        if (_categories.Exists(other => other.Id != categoryId
            && string.Equals(other.Name, normalized.Value, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure(CatalogErrors.DuplicateCategoryName(normalized.Value));
        }

        category.Rename(normalized.Value);
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Elimina una categoria del catalogo.</summary>
    public Result RemoveCategory(Guid categoryId, DateTime utcNow)
    {
        Category? category = _categories.Find(item => item.Id == categoryId);
        if (category is null)
        {
            return Result.Failure(CatalogErrors.CategoryNotFound);
        }

        _categories.Remove(category);
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Indica si una categoria pertenece a este catalogo.</summary>
    public bool HasCategory(Guid categoryId) => _categories.Exists(category => category.Id == categoryId);

    private static Result<string> NormalizeCategoryName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<string>(CatalogErrors.CategoryNameRequired);
        }

        string trimmed = name.Trim();
        return trimmed.Length > MaxCategoryNameLength
            ? Result.Failure<string>(CatalogErrors.CategoryNameTooLong)
            : trimmed;
    }
}
