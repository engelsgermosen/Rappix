namespace Rappix.Notifications.Application.Abstractions;

/// <summary>
/// La infraestructura lanza esta excepcion cuando un guardado choca con un token de concurrencia
/// optimista (xmin). Los consumers la dejan propagar al pipeline de MassTransit para retry exponencial,
/// sin acoplarse a EF Core. En Notifications no es comun (los read-models son pull-through), pero
/// se conserva por simetria con el resto del repo.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    /// <summary>Crea la excepcion con un mensaje por defecto.</summary>
    public ConcurrencyConflictException()
        : base("El registro fue modificado por otra operacion concurrente.")
    {
    }

    /// <summary>Crea la excepcion con un mensaje.</summary>
    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Crea la excepcion con un mensaje y una excepcion interna.</summary>
    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
