using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Identity.Domain;

/// <summary>Errores de dominio relacionados con el usuario.</summary>
public static class UserErrors
{
    /// <summary>Usuario no encontrado.</summary>
    public static readonly Error NotFound =
        Error.NotFound("Identity.User.NotFound", "Usuario no encontrado.");

    /// <summary>Credenciales invalidas (email/telefono o password incorrectos).</summary>
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Identity.User.InvalidCredentials", "Credenciales invalidas.");

    /// <summary>La cuenta esta desactivada.</summary>
    public static readonly Error Inactive =
        Error.Forbidden("Identity.User.Inactive", "La cuenta esta desactivada.");

    /// <summary>El email ya fue confirmado previamente.</summary>
    public static readonly Error EmailAlreadyConfirmed =
        Error.Conflict("Identity.User.EmailAlreadyConfirmed", "El email ya fue confirmado.");

    /// <summary>El usuario no tiene password (cuenta solo-Google) al intentar cambiarla.</summary>
    public static readonly Error NoPasswordSet =
        Error.Conflict("Identity.User.NoPasswordSet", "La cuenta no tiene contrasena local configurada.");

    /// <summary>El email indicado ya esta registrado por otro usuario.</summary>
    public static Error EmailInUse(string email) =>
        Error.Conflict("Identity.User.EmailInUse", $"El email '{email}' ya esta registrado.");

    /// <summary>El telefono indicado ya esta registrado por otro usuario.</summary>
    public static Error PhoneInUse(string phone) =>
        Error.Conflict("Identity.User.PhoneInUse", $"El telefono '{phone}' ya esta registrado.");
}

/// <summary>Errores de validacion del value object Email.</summary>
public static class EmailErrors
{
    /// <summary>El email es obligatorio.</summary>
    public static readonly Error Empty =
        Error.Validation("Identity.Email.Empty", "El email es obligatorio.");

    /// <summary>El email no tiene un formato valido.</summary>
    public static readonly Error Invalid =
        Error.Validation("Identity.Email.Invalid", "El email no tiene un formato valido.");
}

/// <summary>Errores de validacion del value object PhoneNumber.</summary>
public static class PhoneErrors
{
    /// <summary>El telefono es obligatorio.</summary>
    public static readonly Error Empty =
        Error.Validation("Identity.Phone.Empty", "El telefono es obligatorio.");

    /// <summary>El telefono no tiene un formato valido (se espera formato E.164).</summary>
    public static readonly Error Invalid =
        Error.Validation("Identity.Phone.Invalid", "El telefono no tiene un formato valido.");
}

/// <summary>Errores relacionados con tokens (refresh y confirmacion de email).</summary>
public static class TokenErrors
{
    /// <summary>Refresh token no encontrado o invalido. Mensaje opaco a proposito.</summary>
    public static readonly Error InvalidRefresh =
        Error.Unauthorized("Identity.Token.InvalidRefresh", "Sesion invalida. Inicie sesion nuevamente.");

    /// <summary>Refresh token expirado. Mensaje opaco a proposito.</summary>
    public static readonly Error ExpiredRefresh =
        Error.Unauthorized("Identity.Token.ExpiredRefresh", "Sesion invalida. Inicie sesion nuevamente.");

    /// <summary>Reuso de un refresh token ya revocado: posible robo. Mensaje opaco a proposito.</summary>
    public static readonly Error ReuseDetected =
        Error.Unauthorized("Identity.Token.ReuseDetected", "Sesion invalida. Inicie sesion nuevamente.");

    /// <summary>Token de confirmacion de email invalido, usado o expirado.</summary>
    public static readonly Error InvalidConfirmation =
        Error.Validation("Identity.Token.InvalidConfirmation", "Token de confirmacion invalido o expirado.");
}
