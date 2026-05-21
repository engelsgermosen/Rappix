namespace Rappix.Identity.Api.Contracts;

/// <summary>Cuerpo del registro local.</summary>
public sealed record RegisterRequest(
    string Email,
    string? PhoneNumber,
    string Password,
    string FirstName,
    string LastName);

/// <summary>Cuerpo del login local (identificador = email o telefono).</summary>
public sealed record LoginRequest(string Identifier, string Password);

/// <summary>Cuerpo del reenvio de confirmacion de email.</summary>
public sealed record ResendConfirmationRequest(string Email);

/// <summary>Cuerpo de la renovacion de tokens.</summary>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Cuerpo del cierre de sesion.</summary>
public sealed record LogoutRequest(string RefreshToken);
