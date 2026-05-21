namespace Rappix.BuildingBlocks.Messaging.Outbox;

/// <summary>
/// Mensaje persistido en la BD del servicio junto con los cambios de negocio
/// dentro de la misma transacción SQL. Un worker lo lee y lo publica al broker.
/// Garantía: si la transacción se commitea, el mensaje se publica eventualmente.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public required string Type { get; set; }        // FQN del tipo del evento
    public required string Content { get; set; }     // JSON serializado
    public DateTime OccurredOnUtc { get; set; }
    public DateTime? ProcessedOnUtc { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }

    public bool IsProcessed => ProcessedOnUtc.HasValue;
}
