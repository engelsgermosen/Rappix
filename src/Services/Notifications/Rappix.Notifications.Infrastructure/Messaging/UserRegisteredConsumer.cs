using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Identity;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Domain.UserContacts;

namespace Rappix.Notifications.Infrastructure.Messaging;

/// <summary>
/// Proyecta <see cref="UserRegisteredIntegrationEvent"/> a <see cref="UserContact"/>. Upsert: si
/// no existe, crea; si existe (re-entrega legitima o evento posterior con datos actualizados),
/// sobreescribe todos los campos con los del evento mas reciente.
/// </summary>
/// <remarks>
/// El <c>UserType</c> del evento viene como string (<c>"Customer"</c>, <c>"Merchant"</c>,
/// <c>"Courier"</c>, <c>"Admin"</c>); aqui se parsea con <see cref="Enum.TryParse{TEnum}(string?, bool, out TEnum)"/>
/// y se cae a <see cref="UserType.Customer"/> con log de Warning si llega un valor desconocido —
/// el contrato esta fijado en Identity, pero defensa por si en el futuro evolucione.
/// </remarks>
internal sealed partial class UserRegisteredConsumer(
    IUserContactRepository repository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<UserRegisteredConsumer> logger) : IConsumer<UserRegisteredIntegrationEvent>
{
    public async Task Consume(ConsumeContext<UserRegisteredIntegrationEvent> context)
    {
        UserRegisteredIntegrationEvent message = context.Message;
        CancellationToken ct = context.CancellationToken;

        UserType userType = ParseUserType(message.UserType, message.UserId);

        UserContact? existing = await repository.GetByIdAsync(message.UserId, ct).ConfigureAwait(false);
        DateTime utcNow = clock.UtcNow;

        if (existing is null)
        {
            UserContact contact = UserContact.Create(
                userId: message.UserId,
                email: message.Email,
                firstName: message.FirstName,
                lastName: message.LastName,
                userType: userType,
                emailConfirmed: message.EmailConfirmed,
                utcNow: utcNow);
            repository.Add(contact);
            // CA1873: extraer a local antes del Log* — el atributo no inhibe la evaluacion del arg
            // al sitio del log si no es ya un string directo (leccion Tracking/Orders).
            string userTypeName = userType.ToString();
            LogProjected(logger, message.UserId, message.Email, userTypeName);
        }
        else
        {
            existing.Update(
                email: message.Email,
                firstName: message.FirstName,
                lastName: message.LastName,
                userType: userType,
                emailConfirmed: message.EmailConfirmed,
                utcNow: utcNow);
            LogUpdated(logger, message.UserId);
        }

        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private UserType ParseUserType(string raw, Guid userId)
    {
        if (Enum.TryParse(raw, ignoreCase: true, out UserType parsed))
        {
            return parsed;
        }

        LogUnknownUserType(logger, userId, raw ?? "<null>");
        return UserType.Customer;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "UserContact creado userId={UserId} email={Email} userType={UserType}.")]
    private static partial void LogProjected(ILogger logger, Guid userId, string email, string userType);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "UserContact actualizado userId={UserId}.")]
    private static partial void LogUpdated(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "UserRegistered con UserType desconocido userId={UserId} value=\"{Raw}\"; defaulteo a Customer.")]
    private static partial void LogUnknownUserType(ILogger logger, Guid userId, string raw);
}
