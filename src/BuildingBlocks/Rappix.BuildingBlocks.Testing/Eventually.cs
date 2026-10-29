using System.Diagnostics;
using MassTransit.Testing;

namespace Rappix.BuildingBlocks.Testing;

/// <summary>
/// Espera de aserciones eventualmente consistentes (consumers, outbox, proyecciones): sondea una condicion
/// observable hasta que se cumple o vence el timeout, y al vencer FALLA con un mensaje que incluye la
/// condicion, el ultimo estado observado y los consumes con fault del harness. Sustituye a los
/// <c>Task.Delay</c> fijos, a <c>Consumed.Any</c> seguido de una lectura inmediata y a los bucles que
/// terminaban en silencio al agotar el tiempo (falsos verdes).
/// </summary>
/// <remarks>
/// Las excepciones del probe no se reintentan: se propagan tal cual (un error de la base no es "todavia
/// no"). El intervalo solo separa sondeos; no es una espera fija del resultado.
/// </remarks>
public static class Eventually
{
    /// <summary>Timeout por defecto: holgado para Postgres emulado (amd64 en arm64) con las suites en paralelo.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Intervalo por defecto entre sondeos.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Sondea <paramref name="probe"/> hasta que <paramref name="condition"/> se cumple sobre su resultado y lo
    /// devuelve. Al vencer lanza <see cref="EventuallyFailedException"/>.
    /// </summary>
    /// <param name="description">Que se esperaba, en lenguaje natural (va al mensaje de error).</param>
    /// <param name="probe">Lee el estado observable (p. ej. una fila de la base en un scope nuevo).</param>
    /// <param name="condition">Condicion sobre el estado leido.</param>
    /// <param name="timeout">Opcional: tiempo maximo de espera (por defecto <see cref="DefaultTimeout"/>).</param>
    /// <param name="interval">Opcional: pausa entre sondeos (por defecto <see cref="DefaultInterval"/>).</param>
    /// <param name="consumed">Opcional: <c>harness.Consumed</c> o <c>consumer.Consumed</c>, para listar los consumes con fault al fallar.</param>
    /// <param name="describe">Opcional: como mostrar el ultimo estado (por defecto <c>ToString()</c>).</param>
    public static async Task<T> UntilAsync<T>(
        string description,
        Func<Task<T>> probe,
        Func<T, bool> condition,
        TimeSpan? timeout = null,
        TimeSpan? interval = null,
        IReceivedMessageList? consumed = null,
        Func<T, string>? describe = null)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(condition);

        TimeSpan limit = timeout ?? DefaultTimeout;
        TimeSpan pause = interval ?? DefaultInterval;
        var stopwatch = Stopwatch.StartNew();
        int attempts = 0;
        T last;

        while (true)
        {
            attempts++;
            last = await probe();
            if (condition(last))
            {
                return last;
            }

            if (stopwatch.Elapsed >= limit)
            {
                break;
            }

            await Task.Delay(pause);
        }

        string observed = last is null ? "null" : describe?.Invoke(last) ?? last.ToString() ?? "null";
        throw new EventuallyFailedException(
            $"No se cumplio '{description}' en {limit.TotalSeconds:0.#} s ({attempts} sondeos). " +
            $"Ultimo estado observado: {observed}. Consumes con fault: {DescribeFaults(consumed)}.");
    }

    /// <summary>Variante sincrona del probe (p. ej. contar <c>consumer.Consumed</c>).</summary>
    public static Task<T> UntilAsync<T>(
        string description,
        Func<T> probe,
        Func<T, bool> condition,
        TimeSpan? timeout = null,
        TimeSpan? interval = null,
        IReceivedMessageList? consumed = null,
        Func<T, string>? describe = null)
    {
        ArgumentNullException.ThrowIfNull(probe);
        return UntilAsync(description, () => Task.FromResult(probe()), condition, timeout, interval, consumed, describe);
    }

    /// <summary>
    /// Espera a que <paramref name="consumed"/> (<c>harness.Consumed</c> o <c>consumer.Consumed</c>) registre al
    /// menos <paramref name="expected"/> consumos TERMINADOS de mensajes <typeparamref name="T"/> que cumplan
    /// <paramref name="filter"/>, y falla si alguno de ellos termino con fault. A diferencia de
    /// <c>Consumed.Any</c>, no corta por inactividad del harness ni da por bueno un consume con excepcion: al
    /// volver, el consumer ya completo su trabajo (incluido el SaveChanges) sin errores.
    /// </summary>
    /// <remarks>
    /// <paramref name="filter"/> es un <see cref="FilterDelegate{T}"/> para que la lambda se convierta en el
    /// call site (MassTransit no acepta un <c>Func</c> ya almacenado).
    /// </remarks>
    public static async Task<IReadOnlyList<IReceivedMessage<T>>> ConsumedAsync<T>(
        IReceivedMessageList consumed,
        FilterDelegate<IReceivedMessage<T>> filter,
        int expected = 1,
        TimeSpan? timeout = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(consumed);

        IReceivedMessage<T>[] matches = await UntilAsync(
            $"{expected} consumo(s) de {typeof(T).Name}",
            () => consumed.Select(filter).ToArray(),
            received => received.Length >= expected,
            timeout,
            consumed: consumed,
            describe: received => $"{received.Length} consumidos");

        IReceivedMessage<T>[] faulted = matches.Where(received => received.Exception is not null).ToArray();
        if (faulted.Length > 0)
        {
            throw new EventuallyFailedException(
                $"{faulted.Length} de {matches.Length} consumo(s) de {typeof(T).Name} terminaron con fault: " +
                string.Join(" | ", faulted.Select(received => $"{received.Exception!.GetType().Name}: {received.Exception.Message}")),
                faulted[0].Exception!);
        }

        return matches;
    }

    /// <summary>
    /// Lista los consumes con fault (tipo de mensaje + excepcion) de <c>harness.Consumed</c> o
    /// <c>consumer.Consumed</c>. "ninguno" si no hubo faults.
    /// </summary>
    public static string DescribeFaults(IReceivedMessageList? consumed)
    {
        if (consumed is null)
        {
            return "no informado";
        }

        string[] faults = consumed
            .Select(received => received.Exception is not null)
            .Select(received => $"{received.MessageType.Name}: {received.Exception!.GetType().Name}: {received.Exception.Message}")
            .ToArray();

        return faults.Length == 0 ? "ninguno" : string.Join(" | ", faults);
    }
}

/// <summary>La condicion esperada por <see cref="Eventually"/> no se cumplio a tiempo, o el consume esperado termino con fault.</summary>
public sealed class EventuallyFailedException : Exception
{
    public EventuallyFailedException()
    {
    }

    public EventuallyFailedException(string message)
        : base(message)
    {
    }

    public EventuallyFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
