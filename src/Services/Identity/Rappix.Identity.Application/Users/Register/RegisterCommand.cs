using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Identity.Application.Responses;

namespace Rappix.Identity.Application.Users.Register;

/// <summary>Registro local de un usuario. accountType admite "Customer" (default) o "Merchant". Envia email de confirmacion.</summary>
public sealed record RegisterCommand(
    string Email,
    string? PhoneNumber,
    string Password,
    string FirstName,
    string LastName,
    string? AccountType) : IRequest<Result<UserResponse>>;
