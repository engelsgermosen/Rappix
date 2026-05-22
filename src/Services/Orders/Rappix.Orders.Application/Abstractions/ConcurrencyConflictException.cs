namespace Rappix.Orders.Application.Abstractions;

/// <summary>Se lanza cuando un token de concurrencia optimista (xmin) detecta una escritura concurrente.</summary>
public sealed class ConcurrencyConflictException(string message, Exception innerException)
    : Exception(message, innerException);
