using Rappix.BuildingBlocks.Core.Domain;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Domain.Common;
using Rappix.Pricing.Domain.Errors;

namespace Rappix.Pricing.Domain.Surge;

/// <summary>
/// Regla de surge configurable por zona geografica y franja horaria. La demanda real (pedidos vs
/// couriers) vivira en Dispatch (fase futura); hoy el surge es zona + horario x un demand_factor
/// configurable (ADR-0005). Editable por admin. Borrado logico (query filter).
/// </summary>
public sealed class SurgeRule : AggregateRoot<SurgeRuleId>
{
    /// <summary>Hora minima valida de una franja.</summary>
    public const int MinHour = 0;

    /// <summary>Hora maxima valida de una franja (medianoche del dia siguiente).</summary>
    public const int MaxHour = 24;

    private SurgeRule()
    {
    }

    private SurgeRule(
        SurgeRuleId id,
        string? zoneId,
        VerticalType? vertical,
        int startHour,
        int endHour,
        decimal multiplier,
        int priority,
        DateTime utcNow)
        : base(id)
    {
        ZoneId = zoneId;
        Vertical = vertical;
        StartHour = startHour;
        EndHour = endHour;
        Multiplier = multiplier;
        Priority = priority;
        IsActive = true;
        IsDeleted = false;
        CreatedAtUtc = utcNow;
    }

    /// <summary>Zona a la que aplica (null = global, todas las zonas).</summary>
    public string? ZoneId { get; private set; }

    /// <summary>Vertical al que aplica (null = todos los verticales).</summary>
    public VerticalType? Vertical { get; private set; }

    /// <summary>Hora local de inicio de la franja (0-23, inclusivo).</summary>
    public int StartHour { get; private set; }

    /// <summary>Hora local de fin de la franja (1-24, exclusivo).</summary>
    public int EndHour { get; private set; }

    /// <summary>Multiplicador base de la franja (entre 1.0 y el cap configurado).</summary>
    public decimal Multiplier { get; private set; }

    /// <summary>Prioridad de desempate (mayor gana cuando varias reglas coinciden).</summary>
    public int Priority { get; private set; }

    /// <summary>Indica si la regla esta activa.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Borrado logico.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Momento de creacion (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Momento de la ultima actualizacion (UTC).</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Crea una regla validando el rango horario y el multiplicador contra el cap configurado.</summary>
    public static Result<SurgeRule> Create(
        string? zoneId,
        VerticalType? vertical,
        int startHour,
        int endHour,
        decimal multiplier,
        int priority,
        decimal maxMultiplier,
        DateTime utcNow)
    {
        Result invariants = ValidateInvariants(startHour, endHour, multiplier, maxMultiplier);
        if (invariants.IsFailure)
        {
            return Result.Failure<SurgeRule>(invariants.Error);
        }

        string? normalizedZone = string.IsNullOrWhiteSpace(zoneId) ? null : zoneId.Trim();
        return new SurgeRule(SurgeRuleId.New(), normalizedZone, vertical, startHour, endHour, multiplier, priority, utcNow);
    }

    /// <summary>Actualiza los campos editables de la regla.</summary>
    public Result Update(
        string? zoneId,
        VerticalType? vertical,
        int startHour,
        int endHour,
        decimal multiplier,
        int priority,
        bool isActive,
        decimal maxMultiplier,
        DateTime utcNow)
    {
        Result invariants = ValidateInvariants(startHour, endHour, multiplier, maxMultiplier);
        if (invariants.IsFailure)
        {
            return invariants;
        }

        ZoneId = string.IsNullOrWhiteSpace(zoneId) ? null : zoneId.Trim();
        Vertical = vertical;
        StartHour = startHour;
        EndHour = endHour;
        Multiplier = multiplier;
        Priority = priority;
        IsActive = isActive;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Desactiva la regla (borrado logico). Idempotente.</summary>
    public void Deactivate(DateTime utcNow)
    {
        IsActive = false;
        IsDeleted = true;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Indica si la regla aplica a una zona, vertical y hora local dados.</summary>
    public bool Matches(string? zoneId, VerticalType vertical, int hourOfDay)
    {
        bool zoneMatches = ZoneId is null || string.Equals(ZoneId, zoneId, StringComparison.OrdinalIgnoreCase);
        bool verticalMatches = Vertical is null || Vertical == vertical;
        bool hourMatches = hourOfDay >= StartHour && hourOfDay < EndHour;
        return zoneMatches && verticalMatches && hourMatches;
    }

    private static Result ValidateInvariants(int startHour, int endHour, decimal multiplier, decimal maxMultiplier)
    {
        if (startHour < MinHour || endHour > MaxHour || startHour >= endHour)
        {
            return Result.Failure(SurgeErrors.InvalidHourRange);
        }

        return multiplier < 1.0m || multiplier > maxMultiplier
            ? Result.Failure(SurgeErrors.InvalidMultiplier)
            : Result.Success();
    }
}
