using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Identity.Application.Abstractions;
using Rappix.Identity.Application.Authentication;
using Rappix.Identity.Application.Configuration;
using Rappix.Identity.Application.Responses;
using Rappix.Identity.Domain;
using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Application.Users.Register;

/// <summary>Crea el usuario, almacena el hash del token de confirmacion y envia el email de confirmacion.</summary>
internal sealed partial class RegisterCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IEmailSender emailSender,
    IDateTimeProvider clock,
    IOptions<AppOptions> appOptions,
    ILogger<RegisterCommandHandler> logger)
    : IRequestHandler<RegisterCommand, Result<UserResponse>>
{
    private static readonly TimeSpan ConfirmationLifetime = TimeSpan.FromHours(24);
    private readonly AppOptions _app = appOptions.Value;

    public async Task<Result<UserResponse>> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        Result<Email> emailResult = Email.Create(command.Email);
        if (emailResult.IsFailure)
        {
            return Result.Failure<UserResponse>(emailResult.Error);
        }

        PhoneNumber? phone = null;
        if (!string.IsNullOrWhiteSpace(command.PhoneNumber))
        {
            Result<PhoneNumber> phoneResult = PhoneNumber.Create(command.PhoneNumber);
            if (phoneResult.IsFailure)
            {
                return Result.Failure<UserResponse>(phoneResult.Error);
            }

            phone = phoneResult.Value;
        }

        Email email = emailResult.Value;
        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            return Result.Failure<UserResponse>(UserErrors.EmailInUse(email.Value));
        }

        if (phone is not null && await users.PhoneExistsAsync(phone, cancellationToken))
        {
            return Result.Failure<UserResponse>(UserErrors.PhoneInUse(phone.Value));
        }

        DateTime now = clock.UtcNow;
        var user = User.Register(email, phone, passwordHasher.Hash(command.Password), command.FirstName, command.LastName, UserType.Customer, now);

        string rawToken = tokenService.GenerateOpaqueToken();
        user.AddEmailConfirmationToken(tokenService.ComputeHash(rawToken), now, ConfirmationLifetime);

        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        string confirmationUrl = _app.BuildEmailConfirmationUrl(user.Id.Value, rawToken);
        Result emailDelivery = await emailSender.SendEmailConfirmationAsync(user.Email.Value, user.FirstName, confirmationUrl, cancellationToken);
        if (emailDelivery.IsFailure)
        {
            LogConfirmationEmailFailed(logger, user.Email.Value, emailDelivery.Error.Code);
        }

        return UserResponse.From(user);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo enviar el email de confirmacion a {Email}: {ErrorCode}")]
    private static partial void LogConfirmationEmailFailed(ILogger logger, string email, string errorCode);
}
