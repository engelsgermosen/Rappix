using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Identity;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Domain.UserContacts;

namespace Rappix.Notifications.Infrastructure.Messaging;

/// <summary>
/// Aplica <see cref="UserEmailConfirmedIntegrationEvent"/> a la proyeccion <see cref="UserContact"/>:
/// marca <c>EmailConfirmed=true</c>. Idempotente desde true (no-op). Si la proyeccion no existe
/// (cold-start gap: confirmacion llego antes que el UserRegistered se procesara), logea Warning y
/// sale sin crear el contact — el UserRegistered eventualmente llegara y traera EmailConfirmed=true.
/// </summary>
internal sealed partial class UserEmailConfirmedConsumer(
    IUserContactRepository repository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<UserEmailConfirmedConsumer> logger) : IConsumer<UserEmailConfirmedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<UserEmailConfirmedIntegrationEvent> context)
    {
        UserEmailConfirmedIntegrationEvent message = context.Message;
        CancellationToken ct = context.CancellationToken;

        UserContact? existing = await repository.GetByIdAsync(message.UserId, ct).ConfigureAwait(false);
        if (existing is null)
        {
            // Cold-start gap. NO crashear, NO crear sintetico — UserRegistered llegara con todos los
            // campos correctos (incluido EmailConfirmed si el user ya confirmo).
            LogContactMissing(logger, message.UserId);
            return;
        }

        existing.ConfirmEmail(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        LogConfirmed(logger, message.UserId);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "UserEmailConfirmed recibido sin UserContact previo userId={UserId}; skip (cold-start).")]
    private static partial void LogContactMissing(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "UserContact marcado como EmailConfirmed userId={UserId}.")]
    private static partial void LogConfirmed(ILogger logger, Guid userId);
}
