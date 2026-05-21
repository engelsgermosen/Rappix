namespace Rappix.BuildingBlocks.Core.Domain;

public abstract class Entity<TId>
    where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    protected Entity(TId id) => Id = id;
    protected Entity() { }

    public override bool Equals(object? obj)
        => obj is Entity<TId> other && Id.Equals(other.Id);

    public override int GetHashCode() => Id.GetHashCode();
}

/// <summary>
/// Raíz de agregado DDD. Gestiona los eventos de dominio que se publican
/// cuando la unidad de trabajo se persiste.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id) : base(id) { }
    protected AggregateRoot() { }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
        => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
