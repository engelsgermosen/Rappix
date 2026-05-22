using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Catalogs.Create;
using Rappix.Contracts.Merchants;

namespace Rappix.Catalog.Infrastructure.Messaging;

/// <summary>
/// Crea el catalogo (habilitado) cuando un merchant es aprobado. Idempotente (no duplica por merchant);
/// el inbox EF de MassTransit garantiza el procesamiento once-only por mensaje.
/// </summary>
internal sealed partial class MerchantApprovedConsumer(ISender sender, ILogger<MerchantApprovedConsumer> logger)
    : IConsumer<MerchantApprovedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<MerchantApprovedIntegrationEvent> context)
    {
        MerchantApprovedIntegrationEvent message = context.Message;
        LogCreatingCatalog(logger, message.MerchantId, message.VerticalType);

        var command = new CreateCatalogCommand(message.MerchantId, message.OwnerUserId, message.VerticalType);
        Result result = await sender.Send(command, context.CancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"No se pudo crear el catalogo del merchant {message.MerchantId} (error: {result.Error.Code}).");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Creando catalogo para el merchant {MerchantId} (vertical {VerticalType}).")]
    private static partial void LogCreatingCatalog(ILogger logger, Guid merchantId, string verticalType);
}
