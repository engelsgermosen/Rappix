namespace Rappix.Dispatch.Domain.Couriers;

/// <summary>
/// Identificador fuertemente tipado de un courier. Por decision de Fase 6 (ver ADR-0007),
/// el courier es 1-1 con un usuario de Identity, por lo que <see cref="Value"/> es el
/// <c>UserId</c> del JWT. No hay generacion propia: el id viene de UserRegistered.
/// </summary>
public readonly record struct CourierId(Guid Value)
{
    /// <summary>Construye un CourierId desde el UserId de Identity.</summary>
    public static CourierId FromUserId(Guid userId) => new(userId);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
