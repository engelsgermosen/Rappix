using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Catalogs.SetEnabled;
using Rappix.Contracts.Merchants;

namespace Rappix.Catalog.Infrastructure.Messaging;

/// <summary>Deshabilita el catalogo (gating) cuando un merchant es suspendido: sus items dejan de ser comprables.</summary>
internal sealed partial class MerchantSuspendedConsumer(ISender sender, ILogger<MerchantSuspendedConsumer> logger)
    : IConsumer<MerchantSuspendedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<MerchantSuspendedIntegrationEvent> context)
    {
        Guid merchantId = context.Message.MerchantId;
        LogDisabling(logger, merchantId);

        Result result = await sender.Send(new SetCatalogEnabledCommand(merchantId, Enabled: false), context.CancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"No se pudo deshabilitar el catalogo del merchant {merchantId} (error: {result.Error.Code}).");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deshabilitando el catalogo del merchant {MerchantId}.")]
    private static partial void LogDisabling(ILogger logger, Guid merchantId);
}
