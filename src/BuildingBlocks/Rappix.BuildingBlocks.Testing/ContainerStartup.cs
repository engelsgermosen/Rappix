using System.Diagnostics;
using DotNet.Testcontainers.Containers;

namespace Rappix.BuildingBlocks.Testing;

/// <summary>
/// Arranque de contenedores de Testcontainers acotado en concurrencia y en tiempo.
/// </summary>
/// <remarks>
/// <para>
/// Motivo: con <c>dotnet test Rappix.sln</c> los ~10 proyectos de tests corren en paralelo y arrancan a la vez
/// unos 15-20 contenedores <c>postgis/postgis</c> (amd64 emulado en Apple Silicon). Bajo esa rafaga, el
/// <c>docker exec pg_isready</c> con el que Testcontainers 4.1 espera la readiness puede no terminar nunca
/// (dumps con <c>--blame-hang</c>: <c>PostgreSqlBuilder.WaitUntil</c> &gt; <c>DockerContainerOperations.ExecAsync</c>
/// &gt; <c>MultiplexedStream.ReadOutputToEndAsync</c>), y como esa espera no tiene timeout ni token, el
/// fixture se cuelga y con el todo el proyecto.
/// </para>
/// <para>
/// Mitigacion: un semaforo entre procesos (locks de archivo en el directorio temporal) deja arrancar como
/// mucho <see cref="MaxConcurrentStarts"/> contenedores a la vez en toda la maquina, y cada arranque tiene un
/// timeout: si vence, falla con un mensaje claro en lugar de colgar la corrida.
/// </para>
/// </remarks>
public static class ContainerStartup
{
    /// <summary>Arranques simultaneos permitidos en toda la maquina (todos los procesos de test).</summary>
    public const int MaxConcurrentStarts = 3;

    /// <summary>Tiempo maximo de un arranque (incluida la espera de readiness).</summary>
    public static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(3);

    /// <summary>Tiempo maximo esperando un hueco del semaforo.</summary>
    public static readonly TimeSpan SlotTimeout = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan SlotPollInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>Arranca los contenedores en orden, cada uno dentro de un hueco del semaforo y con timeout.</summary>
    public static async Task StartAsync(params IContainer[] containers)
    {
        ArgumentNullException.ThrowIfNull(containers);

        foreach (IContainer container in containers)
        {
            using FileStream slot = await AcquireSlotAsync();
            try
            {
                await container.StartAsync().WaitAsync(StartTimeout);
            }
            catch (TimeoutException exception)
            {
                throw new TimeoutException(
                    $"El contenedor {container.Image.FullName} ({container.Name}) no arranco en " +
                    $"{StartTimeout.TotalMinutes:0} min (estado: {container.State}). Docker puede estar saturado.",
                    exception);
            }
        }
    }

    private static async Task<FileStream> AcquireSlotAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            for (int index = 0; index < MaxConcurrentStarts; index++)
            {
                string path = Path.Combine(Path.GetTempPath(), $"rappix-testcontainers-start-{index}.lock");
                try
                {
                    // FileShare.None toma un lock exclusivo (flock en Unix); se libera al cerrar el stream o si
                    // el proceso muere.
                    return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    // Hueco ocupado por otro arranque: se prueba el siguiente.
                }
            }

            if (stopwatch.Elapsed >= SlotTimeout)
            {
                throw new TimeoutException(
                    $"No hubo hueco para arrancar un contenedor en {SlotTimeout.TotalMinutes:0} min " +
                    $"({MaxConcurrentStarts} arranques simultaneos ocupados).");
            }

            await Task.Delay(SlotPollInterval);
        }
    }
}
