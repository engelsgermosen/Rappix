using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Rappix.Notifications.Infrastructure.Persistence;

/// <summary>
/// Helper para reconocer una violacion de UNIQUE CONSTRAINT de PostgreSQL en una
/// <see cref="DbUpdateException"/> y decidir si corresponde a un constraint especifico. Convertir
/// una violacion concreta (e.g. <c>UX_Notification_BusinessKey</c>) a un comportamiento de no-op
/// idempotente NO debe tragarse otros errores de BD — por eso este helper exige el
/// <c>constraintName</c> exacto y la combinacion <c>SqlState=="23505"</c>.
/// </summary>
internal static class PostgresUniqueViolation
{
    /// <summary>SqlState de PostgreSQL para violacion de unique constraint.</summary>
    private const string UniqueViolationSqlState = "23505";

    /// <summary>
    /// Devuelve <c>true</c> si la excepcion es un <see cref="DbUpdateException"/> cuya causa raiz
    /// es una violacion de UNIQUE sobre EL constraint indicado. Cualquier otro escenario (otra
    /// constraint, FK violation, NOT NULL, conexion perdida) devuelve <c>false</c> para que el caller
    /// propague la excepcion original — DEFENSIVE: no convertir errores no relacionados en no-ops.
    /// </summary>
    public static bool Is(Exception exception, string constraintName)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(constraintName);

        if (exception is not DbUpdateException dbEx)
        {
            return false;
        }

        return dbEx.InnerException is PostgresException pgEx
            && string.Equals(pgEx.SqlState, UniqueViolationSqlState, StringComparison.Ordinal)
            && string.Equals(pgEx.ConstraintName, constraintName, StringComparison.Ordinal);
    }
}
