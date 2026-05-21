using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Contracts.Identity;
using Rappix.Merchants.Application.Merchants.CreateDraftFromUser;

namespace Rappix.Merchants.Infrastructure.Messaging;

/// <summary>
/// Escucha el registro de usuarios de Identity (choreography). Si el usuario se registro como
/// Merchant, crea su comercio en estado Draft. La creacion es idempotente (no duplica por owner) y
/// el inbox EF de MassTransit garantiza el procesamiento once-only por mensaje.
/// </summary>
internal sealed partial class UserRegisteredConsumer(ISender sender, ILogger<UserRegisteredConsumer> logger)
    : IConsumer<UserRegisteredIntegrationEvent>
{
    private const string MerchantUserType = "Merchant";

    public async Task Consume(ConsumeContext<UserRegisteredIntegrationEvent> context)
    {
        UserRegisteredIntegrationEvent message = context.Message;
        if (!string.Equals(message.UserType, MerchantUserType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        LogCreatingDraft(logger, message.UserId);

        var command = new CreateDraftMerchantCommand(message.UserId, message.FirstName);
        Result result = await sender.Send(command, context.CancellationToken);
        if (result.IsFailure)
        {
            // Lanzar fuerza el retry/redelivery de MassTransit; el inbox EF garantiza el once-only.
            throw new InvalidOperationException(
                $"No se pudo crear el merchant Draft para el usuario {message.UserId} (error: {result.Error.Code}).");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Creando merchant Draft para el usuario {UserId} registrado como Merchant.")]
    private static partial void LogCreatingDraft(ILogger logger, Guid userId);
}
