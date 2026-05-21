namespace Rappix.Identity.Api.Contracts;

/// <summary>Cuerpo de la actualizacion de perfil.</summary>
public sealed record UpdateProfileRequest(string FirstName, string LastName, string? PhoneNumber);

/// <summary>Cuerpo del cambio de contrasena.</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
