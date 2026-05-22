namespace Rappix.Orders.Application.Abstractions;

/// <summary>Unidad de trabajo: persiste los cambios del agregado y el estado de la saga en una transaccion.</summary>
public interface IUnitOfWork
{
    /// <summary>Guarda los cambios pendientes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
