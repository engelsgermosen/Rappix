using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Merchants;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Domain.MerchantContacts;

namespace Rappix.Notifications.Infrastructure.Messaging;

/// <summary>
/// Consumer multi-IConsumer que proyecta los 4 integration events de lifecycle de Merchants a la
/// tabla <see cref="MerchantContact"/>. Todos los eventos llevan <c>MerchantId + OwnerUserId</c>;
/// la accion es identica: upsert del mapping. El primer evento que llegue (normalmente
/// <c>MerchantApprovedIntegrationEvent</c>, que es el unico que precede a <c>OrderSubmitted</c>
/// porque un merchant no puede recibir pedidos sin estar Approved) crea la fila; los siguientes
/// (Activated/Rejected/Suspended) son no-op idempotente si el OwnerUserId no cambio.
/// </summary>
/// <remarks>
/// Suspension/Rejection NO borran el contact — el merchant suspendido sigue siendo notificable
/// (e.g. "tu merchant fue rechazado, contacta soporte" es exactamente la notificacion que
/// MerchantRejected dispara, follow-up de Fase 9 si se decide notificar lifecycle de merchants).
/// </remarks>
internal sealed partial class MerchantLifecycleConsumer(
    IMerchantContactRepository repository,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<MerchantLifecycleConsumer> logger)
    : IConsumer<MerchantApprovedIntegrationEvent>,
      IConsumer<MerchantActivatedIntegrationEvent>,
      IConsumer<MerchantRejectedIntegrationEvent>,
      IConsumer<MerchantSuspendedIntegrationEvent>
{
    public Task Consume(ConsumeContext<MerchantApprovedIntegrationEvent> context) =>
        UpsertAsync(context.Message.MerchantId, context.Message.OwnerUserId, "Approved", context.CancellationToken);

    public Task Consume(ConsumeContext<MerchantActivatedIntegrationEvent> context) =>
        UpsertAsync(context.Message.MerchantId, context.Message.OwnerUserId, "Activated", context.CancellationToken);

    public Task Consume(ConsumeContext<MerchantRejectedIntegrationEvent> context) =>
        UpsertAsync(context.Message.MerchantId, context.Message.OwnerUserId, "Rejected", context.CancellationToken);

    public Task Consume(ConsumeContext<MerchantSuspendedIntegrationEvent> context) =>
        UpsertAsync(context.Message.MerchantId, context.Message.OwnerUserId, "Suspended", context.CancellationToken);

    private async Task UpsertAsync(Guid merchantId, Guid ownerUserId, string sourceEvent, CancellationToken cancellationToken)
    {
        MerchantContact? existing = await repository.GetByIdAsync(merchantId, cancellationToken).ConfigureAwait(false);
        DateTime utcNow = clock.UtcNow;

        if (existing is null)
        {
            MerchantContact contact = MerchantContact.Create(merchantId, ownerUserId, utcNow);
            repository.Add(contact);
            LogProjected(logger, merchantId, ownerUserId, sourceEvent);
        }
        else
        {
            existing.Update(ownerUserId, utcNow);
            LogUpdated(logger, merchantId, sourceEvent);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "MerchantContact creado merchantId={MerchantId} ownerUserId={OwnerUserId} via={SourceEvent}.")]
    private static partial void LogProjected(ILogger logger, Guid merchantId, Guid ownerUserId, string sourceEvent);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "MerchantContact actualizado merchantId={MerchantId} via={SourceEvent}.")]
    private static partial void LogUpdated(ILogger logger, Guid merchantId, string sourceEvent);
}
