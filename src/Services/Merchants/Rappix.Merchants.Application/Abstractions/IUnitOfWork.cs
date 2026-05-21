namespace Rappix.Merchants.Application.Abstractions;

/// <summary>Confirma los cambios pendientes del contexto de persistencia en una unica transaccion.</summary>
public interface IUnitOfWork
{
    /// <summary>Persiste los cambios pendientes y devuelve el numero de filas afectadas.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
