using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Merchants.Domain.Merchants;

/// <summary>
/// Un rango horario de un dia. Un merchant puede tener varios rangos por dia (p.ej. manana y tarde
/// con cierre intermedio) representados como filas separadas.
/// </summary>
public sealed class OperatingHours : Entity<Guid>
{
    private OperatingHours()
    {
    }

    internal OperatingHours(MerchantId merchantId, DayOfWeek dayOfWeek, TimeOnly opensAt, TimeOnly closesAt)
        : base(Guid.CreateVersion7())
    {
        MerchantId = merchantId;
        DayOfWeek = dayOfWeek;
        OpensAt = opensAt;
        ClosesAt = closesAt;
    }

    /// <summary>Merchant propietario.</summary>
    public MerchantId MerchantId { get; private set; }

    /// <summary>Dia de la semana.</summary>
    public DayOfWeek DayOfWeek { get; private set; }

    /// <summary>Hora de apertura.</summary>
    public TimeOnly OpensAt { get; private set; }

    /// <summary>Hora de cierre.</summary>
    public TimeOnly ClosesAt { get; private set; }
}
