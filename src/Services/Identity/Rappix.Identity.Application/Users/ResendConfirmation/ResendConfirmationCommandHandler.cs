using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Identity.Application.Abstractions;
using Rappix.Identity.Application.Authentication;
using Rappix.Identity.Application.Configuration;
using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Application.Users.ResendConfirmation;

/// <summary>Genera un nuevo token de confirmacion y reenvia el email, si procede.</summary>
internal sealed partial class ResendConfirmationCommandHandler(
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ITokenService tokenService,
    IEmailSender emailSender,
    IDateTimeProvider clock,
    IOptions<AppOptions> appOptions,
    ILogger<ResendConfirmationCommandHandler> logger)
    : IRequestHandler<ResendConfirmationCommand, Result>
{
    private static readonly TimeSpan ConfirmationLifetime = TimeSpan.FromHours(24);
    private readonly AppOptions _app = appOptions.Value;

    public async Task<Result> Handle(ResendConfirmationCommand command, CancellationToken cancellationToken)
    {
        Result<Email> emailResult = Email.Create(command.Email);
        if (emailResult.IsFailure)
        {
            return Result.Success();
        }

        User? user = await users.GetByEmailAsync(emailResult.Value, cancellationToken);
        if (user is null || user.EmailConfirmed)
        {
            return Result.Success();
        }

        DateTime now = clock.UtcNow;
        string rawToken = tokenService.GenerateOpaqueToken();
        user.AddEmailConfirmationToken(tokenService.ComputeHash(rawToken), now, ConfirmationLifetime);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        string confirmationUrl = _app.BuildEmailConfirmationUrl(user.Id.Value, rawToken);
        Result emailDelivery = await emailSender.SendEmailConfirmationAsync(user.Email.Value, user.FirstName, confirmationUrl, cancellationToken);
        if (emailDelivery.IsFailure)
        {
            LogConfirmationEmailFailed(logger, user.Email.Value, emailDelivery.Error.Code);
        }

        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo reenviar el email de confirmacion a {Email}: {ErrorCode}")]
    private static partial void LogConfirmationEmailFailed(ILogger logger, string email, string errorCode);
}
