using System.Net.Sockets;
using Npgsql;

namespace Rappix.BuildingBlocks.WebApi.Persistence;

/// <summary>
/// Reconoce errores TRANSITORIOS de "Postgres aun no acepta conexiones" durante el arranque
/// del contenedor. Devuelve <c>true</c> SOLO para:
///   - <see cref="PostgresException"/> con SqlState "57P03" (cannot_connect_now / database system is starting up)
///   - Cualquier excepcion cuya cadena de InnerException contenga un <see cref="SocketException"/>
///     (connection refused / no route to host: el contenedor Postgres aun no abrio el puerto 5432).
/// Cualquier otro error (SQL invalido en una migracion, constraint roto, schema inconsistente,
/// FK violation, timeout post-conexion) devuelve <c>false</c> para que el caller propague —
/// DEFENSIVE: no esconder bugs reales de migracion detras de N reintentos fallidos. Mismo
/// principio que <c>PostgresUniqueViolation</c> en Notifications.
/// </summary>
internal static class PostgresStartupError
{
    /// <summary>SqlState de PostgreSQL para "cannot_connect_now" (la BD esta arrancando).</summary>
    private const string CannotConnectNowSqlState = "57P03";

    public static bool IsTransientStartupError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // Caso 1: Postgres respondio TCP pero aun esta inicializando (post-recovery, WAL replay, etc.).
        // El SqlState 57P03 es el codigo oficial que la BD envia para decir "vuelve pronto".
        if (exception is PostgresException pgEx
            && string.Equals(pgEx.SqlState, CannotConnectNowSqlState, StringComparison.Ordinal))
        {
            return true;
        }

        // Caso 2: Postgres todavia no acepta TCP (connection refused, host unreachable, ECONNREFUSED).
        // Npgsql envuelve el SocketException dentro de un NpgsqlException (o varias capas);
        // recorrer la cadena de InnerException para detectarlo a cualquier profundidad.
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException) return true;
        }

        return false;
    }
}
