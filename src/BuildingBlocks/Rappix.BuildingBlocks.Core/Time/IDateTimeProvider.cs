namespace Rappix.BuildingBlocks.Core.Time;

/// <summary>
/// Abstracción sobre DateTime.UtcNow para permitir tests determinísticos.
/// Inyectar siempre en lugar de usar DateTime.UtcNow directamente.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateOnly TodayUtc { get; }
}

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);
}
