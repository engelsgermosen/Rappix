namespace Rappix.BuildingBlocks.Core.Domain;

/// <summary>
/// Marca un evento que ocurre dentro del dominio (no sale del proceso del servicio).
/// Se publica vía MediatR para reaccionar internamente.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
