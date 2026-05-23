namespace Rappix.Dispatch.Application.Abstractions;

/// <summary>
/// La infraestructura lanza esta excepcion cuando un guardado choca con el token de concurrencia
/// optimista (xmin). Los manejadores la traducen a un Error.Conflict sin acoplarse a EF Core.
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
