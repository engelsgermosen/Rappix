namespace Rappix.BuildingBlocks.Core.Results;

public enum ErrorType
{
    None = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,
    Failure = 6
}

/// <summary>
/// Error tipado para usar con Result. El Code debe seguir el patrón "Service.Resource.Reason".
/// Ejemplos: "Identity.User.NotFound", "Orders.Order.AlreadyAccepted", "Catalog.Item.OutOfStock".
/// </summary>
public sealed record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);
    public static readonly Error NullValue = new("General.Null", "Se proporcionó un valor nulo.", ErrorType.Failure);

    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);
    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);
    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);
    public static Error Unauthorized(string code, string description) => new(code, description, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string description) => new(code, description, ErrorType.Forbidden);
    public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);
}
