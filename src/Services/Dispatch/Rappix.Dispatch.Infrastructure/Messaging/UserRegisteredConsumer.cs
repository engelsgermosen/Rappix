using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Contracts.Identity;
using Rappix.Dispatch.Application.Couriers.CreateDraftFromUser;

namespace Rappix.Dispatch.Infrastructure.Messaging;

/// <summary>
/// Escucha el registro de usuarios de Identity (choreography). Si el usuario se registro como
/// Courier, crea su perfil en Offline. La creacion es idempotente (no duplica por UserId) y el
/// inbox EF de MassTransit garantiza el procesamiento once-only por mensaje.
/// </summary>
internal sealed partial class UserRegisteredConsumer(ISender sender, ILogger<UserRegisteredConsumer> logger)
    : IConsumer<UserRegisteredIntegrationEvent>
{
    private const string CourierUserType = "Courier";

    public async Task Consume(ConsumeContext<UserRegisteredIntegrationEvent> context)
    {
        UserRegisteredIntegrationEvent message = context.Message;
        if (!string.Equals(message.UserType, CourierUserType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        LogCreatingDraft(logger, message.UserId);

        var command = new CreateDraftCourierCommand(message.UserId, message.FirstName);
        Result result = await sender.Send(command, context.CancellationToken);
        if (result.IsFailure)
        {
            // Lanzar fuerza el retry/redelivery de MassTransit; el inbox EF garantiza el once-only.
            throw new InvalidOperationException(
                $"No se pudo crear el courier Draft para el usuario {message.UserId} (error: {result.Error.Code}).");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Creando courier Draft para el usuario {UserId} registrado como Courier.")]
    private static partial void LogCreatingDraft(ILogger logger, Guid userId);
}
