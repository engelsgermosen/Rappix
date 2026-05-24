using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Rappix.BuildingBlocks.WebApi.Persistence;

/// <summary>
/// Helpers de bootstrap para aplicar migraciones EF Core al arranque del servicio,
/// resilientes a la ventana en la que Postgres aun esta inicializando (docker compose up).
/// </summary>
public static partial class MigrationExtensions
{
    // Backoff exponencial: 1s, 2s, 4s, 8s, 16s, 32s ENTRE reintentos. Con el intento inicial
    // suman 7 attempts totales y ~63s esperando a Postgres antes de crashear. Cubre arranques
    // lentos (volumenes nuevos en docker compose up -v, primera vez con docker compose up,
    // CI con cold start del runner). El ultimo retry de 32s da un chance generoso.
    private static readonly TimeSpan[] BackoffDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(16),
        TimeSpan.FromSeconds(32),
    ];

    /// <summary>
    /// Aplica las migraciones EF Core del <typeparamref name="TDbContext"/> al arrancar el servicio,
    /// con retry + backoff exponencial SOLO ante errores transitorios de "Postgres aun no acepta
    /// conexiones" (SqlState 57P03 o SocketException anidada — ver <c>PostgresStartupError</c>).
    /// Cualquier otra excepcion (SQL invalido en una migracion, constraint roto, schema inconsistente)
    /// propaga inmediatamente sin reintento: crash rapido y claro, NUNCA tragar errores reales.
    /// </summary>
    /// <remarks>
    /// No-op si el entorno es "Testing": los <c>WebApplicationFactory</c> migran explicitamente en su
    /// <c>InitializeAsync</c> contra Testcontainers, preservando el contrato actual de los tests.
    /// El guard vive aqui (no en cada Program.cs) para que la llamada sea de UNA linea por servicio.
    /// </remarks>
    public static async Task MigrateDbContextWithRetryAsync<TDbContext>(this WebApplication app)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(app);

        if (app.Environment.IsEnvironment("Testing"))
        {
            return;
        }

        string dbContextName = typeof(TDbContext).Name;

        ILogger logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger($"Rappix.Migrations.{dbContextName}");

        int totalAttempts = BackoffDelays.Length + 1;
        for (int attempt = 1; attempt <= totalAttempts; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                TDbContext db = scope.ServiceProvider.GetRequiredService<TDbContext>();
                await db.Database.MigrateAsync().ConfigureAwait(false);

                if (attempt > 1)
                {
                    LogMigrationsAppliedAfterRetry(logger, dbContextName, attempt, totalAttempts);
                }
                return;
            }
            catch (Exception ex) when (PostgresStartupError.IsTransientStartupError(ex) && attempt < totalAttempts)
            {
                TimeSpan delay = BackoffDelays[attempt - 1];
                double delaySeconds = delay.TotalSeconds;
                LogPostgresNotReady(logger, ex, dbContextName, delaySeconds, attempt, totalAttempts);
                await Task.Delay(delay).ConfigureAwait(false);
            }
            catch (Exception ex) when (PostgresStartupError.IsTransientStartupError(ex))
            {
                // Agotados los reintentos transitorios — la BD genuinamente no llego a tiempo.
                // Log explicito ANTES de propagar para que el crash sea diagnostico, no confuso.
                double totalWaited = BackoffDelays.Sum(d => d.TotalSeconds);
                LogStartupTimeout(logger, ex, totalAttempts, totalWaited, dbContextName);
                throw;
            }
            // Cualquier OTRA excepcion (SQL invalido en migracion, constraint roto, FK rota, schema
            // inconsistente) NO entra a los catch de arriba → propaga inmediatamente sin reintento.
            // Es exactamente lo que queremos: un error REAL de migracion debe crashear claro y rapido.
        }
    }

    // ----- LoggerMessage source generators: evitan boxing/CA1873 y son el patron establecido del repo. -----

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Migraciones de {DbContext} aplicadas en intento {Attempt}/{Total} (BD respondio tras esperar el arranque).")]
    private static partial void LogMigrationsAppliedAfterRetry(
        ILogger logger, string dbContext, int attempt, int total);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Postgres aun no esta listo para {DbContext}. Reintentando en {DelaySeconds}s (intento {Attempt}/{Total}).")]
    private static partial void LogPostgresNotReady(
        ILogger logger, Exception exception, string dbContext, double delaySeconds, int attempt, int total);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error,
        Message = "Postgres no acepto conexiones tras {Total} intentos (~{TotalSeconds}s acumulados) para {DbContext}. Abortando arranque.")]
    private static partial void LogStartupTimeout(
        ILogger logger, Exception exception, int total, double totalSeconds, string dbContext);
}
