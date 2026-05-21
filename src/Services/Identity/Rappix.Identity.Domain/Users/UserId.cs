namespace Rappix.Identity.Domain.Users;

/// <summary>Identificador fuertemente tipado de un usuario (UUIDv7 ordenado temporalmente).</summary>
public readonly record struct UserId(Guid Value)
{
    /// <summary>Genera un nuevo identificador basado en UUIDv7.</summary>
    public static UserId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
