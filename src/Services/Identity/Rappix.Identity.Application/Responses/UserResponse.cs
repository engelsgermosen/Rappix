using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Application.Responses;

/// <summary>Representacion publica de un usuario.</summary>
public sealed record UserResponse(
    Guid Id,
    string Email,
    string? PhoneNumber,
    string FirstName,
    string LastName,
    string UserType,
    bool EmailConfirmed,
    bool PhoneConfirmed,
    DateTime CreatedAtUtc)
{
    /// <summary>Proyecta un agregado User a su representacion publica.</summary>
    public static UserResponse From(User user) => new(
        user.Id.Value,
        user.Email.Value,
        user.PhoneNumber?.Value,
        user.FirstName,
        user.LastName,
        user.UserType.ToString(),
        user.EmailConfirmed,
        user.PhoneConfirmed,
        user.CreatedAtUtc);
}
