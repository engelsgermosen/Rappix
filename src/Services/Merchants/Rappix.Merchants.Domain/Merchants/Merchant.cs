using NetTopologySuite.Geometries;
using Rappix.BuildingBlocks.Core.Domain;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Domain.Abstractions;
using Rappix.Merchants.Domain.Merchants.Events;

namespace Rappix.Merchants.Domain.Merchants;

/// <summary>
/// Agregado raiz de un comercio. Gestiona su maquina de estados, zonas de cobertura y horarios.
/// </summary>
public sealed class Merchant : AggregateRoot<MerchantId>, IHasDomainEvents
{
    private readonly List<ServiceArea> _serviceAreas = [];
    private readonly List<OperatingHours> _operatingHours = [];

    private Merchant()
    {
    }

    private Merchant(
        MerchantId id,
        Guid ownerUserId,
        string name,
        Slug slug,
        VerticalType verticalType,
        CommissionPercentage commission,
        DateTime utcNow)
        : base(id)
    {
        OwnerUserId = ownerUserId;
        Name = name;
        Slug = slug;
        VerticalType = verticalType;
        CommissionPercentage = commission;
        Status = MerchantStatus.Draft;
        TotalReviews = 0;
        IsDeleted = false;
        CreatedAtUtc = utcNow;
    }

    /// <summary>Usuario (Identity) propietario del comercio.</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>Nombre comercial.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Slug unico.</summary>
    public Slug Slug { get; private set; } = null!;

    /// <summary>RNC (obligatorio para enviar a aprobacion).</summary>
    public Rnc? Rnc { get; private set; }

    /// <summary>Descripcion.</summary>
    public string? Description { get; private set; }

    /// <summary>Vertical de negocio.</summary>
    public VerticalType VerticalType { get; private set; }

    /// <summary>Estado actual.</summary>
    public MerchantStatus Status { get; private set; }

    /// <summary>Comision negociada (editable solo por admin).</summary>
    public CommissionPercentage CommissionPercentage { get; private set; } = null!;

    /// <summary>Calificacion promedio cacheada. TODO(Fase 5): actualizada por el consumer de Ratings.</summary>
    public decimal? AverageRating { get; private set; }

    /// <summary>Total de reseñas cacheado. TODO(Fase 5).</summary>
    public int TotalReviews { get; private set; }

    /// <summary>Key del logo en MinIO (bucket merchants-logos).</summary>
    public string? LogoObjectKey { get; private set; }

    /// <summary>Razon del rechazo, si aplica.</summary>
    public string? RejectionReason { get; private set; }

    /// <summary>Razon de la suspension, si aplica.</summary>
    public string? SuspensionReason { get; private set; }

    /// <summary>Borrado logico.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Momento del borrado logico (UTC).</summary>
    public DateTime? DeletedAtUtc { get; private set; }

    /// <summary>Momento de creacion (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Momento de la ultima actualizacion (UTC).</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Zonas de cobertura.</summary>
    public IReadOnlyCollection<ServiceArea> ServiceAreas => _serviceAreas.AsReadOnly();

    /// <summary>Horarios de atencion.</summary>
    public IReadOnlyCollection<OperatingHours> OperatingHours => _operatingHours.AsReadOnly();

    /// <summary>Indica si el comercio esta activo (visible a clientes).</summary>
    public bool IsActive => Status == MerchantStatus.Active;

    /// <summary>Crea un comercio en estado Draft (lo invoca el consumer al registrarse un Merchant).</summary>
    public static Merchant CreateDraft(
        Guid ownerUserId,
        string name,
        Slug slug,
        VerticalType verticalType,
        CommissionPercentage commission,
        DateTime utcNow) =>
        new(MerchantId.New(), ownerUserId, name.Trim(), slug, verticalType, commission, utcNow);

    /// <summary>Actualiza el perfil (owner).</summary>
    public Result UpdateProfile(string name, Slug slug, Rnc? rnc, string? description, VerticalType verticalType, DateTime utcNow)
    {
        Result editable = EnsureEditable("editar");
        if (editable.IsFailure)
        {
            return editable;
        }

        Name = name.Trim();
        Slug = slug;
        Rnc = rnc;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        VerticalType = verticalType;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Reemplaza todos los horarios (bulk).</summary>
    public Result ReplaceOperatingHours(IReadOnlyCollection<OperatingHoursRange> ranges, DateTime utcNow)
    {
        Result editable = EnsureEditable("editar");
        if (editable.IsFailure)
        {
            return editable;
        }

        if (ranges.Any(range => range.OpensAt >= range.ClosesAt))
        {
            return Result.Failure(OperatingHoursErrors.InvalidRange);
        }

        _operatingHours.Clear();
        foreach (OperatingHoursRange range in ranges)
        {
            _operatingHours.Add(new OperatingHours(Id, range.DayOfWeek, range.OpensAt, range.ClosesAt));
        }

        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Agrega una zona de cobertura poligonal.</summary>
    public Result AddPolygonServiceArea(Polygon polygon, DateTime utcNow)
    {
        Result editable = EnsureEditable("editar");
        if (editable.IsFailure)
        {
            return editable;
        }

        Result<ServiceArea> area = ServiceArea.CreatePolygon(Id, polygon, utcNow);
        if (area.IsFailure)
        {
            return Result.Failure(area.Error);
        }

        _serviceAreas.Add(area.Value);
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Agrega una zona de cobertura circular.</summary>
    public Result AddCircleServiceArea(Point center, int radiusMeters, DateTime utcNow)
    {
        Result editable = EnsureEditable("editar");
        if (editable.IsFailure)
        {
            return editable;
        }

        Result<ServiceArea> area = ServiceArea.CreateCircle(Id, center, radiusMeters, utcNow);
        if (area.IsFailure)
        {
            return Result.Failure(area.Error);
        }

        _serviceAreas.Add(area.Value);
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Quita una zona de cobertura.</summary>
    public Result RemoveServiceArea(Guid serviceAreaId, DateTime utcNow)
    {
        Result editable = EnsureEditable("editar");
        if (editable.IsFailure)
        {
            return editable;
        }

        ServiceArea? area = _serviceAreas.FirstOrDefault(serviceArea => serviceArea.Id == serviceAreaId);
        if (area is null)
        {
            return Result.Failure(MerchantErrors.ServiceAreaNotFound);
        }

        _serviceAreas.Remove(area);
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Asigna la key del logo (la validacion de imagen ocurre antes, en la capa de aplicacion).</summary>
    public void SetLogo(string objectKey, DateTime utcNow)
    {
        LogoObjectKey = objectKey;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Envia a aprobacion (Draft -> Pending). Valida completitud.</summary>
    public Result SubmitForApproval(DateTime utcNow)
    {
        if (Status != MerchantStatus.Draft)
        {
            return Result.Failure(MerchantErrors.InvalidTransition(Status, "enviar a aprobacion"));
        }

        if (Rnc is null || _serviceAreas.Count == 0 || _operatingHours.Count == 0)
        {
            return Result.Failure(MerchantErrors.IncompleteForSubmission);
        }

        Status = MerchantStatus.Pending;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Aprueba (admin): Pending -> Active.</summary>
    public Result Approve(DateTime utcNow)
    {
        if (Status != MerchantStatus.Pending)
        {
            return Result.Failure(MerchantErrors.InvalidTransition(Status, "aprobar"));
        }

        Status = MerchantStatus.Active;
        UpdatedAtUtc = utcNow;
        RaiseDomainEvent(new MerchantApprovedDomainEvent(Id, OwnerUserId, Name, Slug.Value, VerticalType));
        return Result.Success();
    }

    /// <summary>Rechaza (admin): Pending -> Rejected.</summary>
    public Result Reject(string reason, DateTime utcNow)
    {
        if (Status != MerchantStatus.Pending)
        {
            return Result.Failure(MerchantErrors.InvalidTransition(Status, "rechazar"));
        }

        Status = MerchantStatus.Rejected;
        RejectionReason = reason;
        UpdatedAtUtc = utcNow;
        RaiseDomainEvent(new MerchantRejectedDomainEvent(Id, OwnerUserId, reason));
        return Result.Success();
    }

    /// <summary>Suspende (admin): Active/Paused -> Suspended.</summary>
    public Result Suspend(string reason, DateTime utcNow)
    {
        if (Status is not (MerchantStatus.Active or MerchantStatus.Paused))
        {
            return Result.Failure(MerchantErrors.InvalidTransition(Status, "suspender"));
        }

        Status = MerchantStatus.Suspended;
        SuspensionReason = reason;
        UpdatedAtUtc = utcNow;
        RaiseDomainEvent(new MerchantSuspendedDomainEvent(Id, OwnerUserId, reason));
        return Result.Success();
    }

    /// <summary>Reactiva (admin): Suspended -> Active.</summary>
    public Result Unsuspend(DateTime utcNow)
    {
        if (Status != MerchantStatus.Suspended)
        {
            return Result.Failure(MerchantErrors.InvalidTransition(Status, "reactivar"));
        }

        Status = MerchantStatus.Active;
        SuspensionReason = null;
        UpdatedAtUtc = utcNow;
        RaiseDomainEvent(new MerchantActivatedDomainEvent(Id, OwnerUserId));
        return Result.Success();
    }

    /// <summary>Pausa temporal: Active -> Paused.</summary>
    public Result Pause(DateTime utcNow)
    {
        if (Status != MerchantStatus.Active)
        {
            return Result.Failure(MerchantErrors.InvalidTransition(Status, "pausar"));
        }

        Status = MerchantStatus.Paused;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Reanuda desde pausa: Paused -> Active.</summary>
    public Result Resume(DateTime utcNow)
    {
        if (Status != MerchantStatus.Paused)
        {
            return Result.Failure(MerchantErrors.InvalidTransition(Status, "reanudar"));
        }

        Status = MerchantStatus.Active;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Actualiza la comision (admin).</summary>
    public void UpdateCommission(CommissionPercentage commission, DateTime utcNow)
    {
        CommissionPercentage = commission;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Borrado logico.</summary>
    public void Delete(DateTime utcNow)
    {
        IsDeleted = true;
        DeletedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    private Result EnsureEditable(string action) =>
        Status is MerchantStatus.Suspended or MerchantStatus.Rejected
            ? Result.Failure(MerchantErrors.InvalidTransition(Status, action))
            : Result.Success();
}

/// <summary>Rango horario de entrada para reemplazar horarios.</summary>
public readonly record struct OperatingHoursRange(DayOfWeek DayOfWeek, TimeOnly OpensAt, TimeOnly ClosesAt);
