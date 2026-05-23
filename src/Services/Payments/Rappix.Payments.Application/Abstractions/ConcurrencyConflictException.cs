namespace Rappix.Payments.Application.Abstractions;

/// <summary>
/// La infraestructura lanza esta excepcion cuando un guardado choca con el token de concurrencia
/// optimista (xmin). Los manejadores y consumers la traducen a un Error.Conflict (Payments) o la
/// dejan propagar al pipeline de MassTransit para retry exponencial, sin acoplarse a EF Core.
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
