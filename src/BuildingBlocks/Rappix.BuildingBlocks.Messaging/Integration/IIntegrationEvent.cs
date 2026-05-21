namespace Rappix.BuildingBlocks.Messaging.Integration;

/// <summary>
/// Marca un evento que cruza el límite del servicio (viaja por RabbitMQ).
/// A diferencia de IDomainEvent, este se serializa y publica al broker.
/// Los eventos de integración deben estar versionados y ser estables en el tiempo.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
}

public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
