using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Catalogs.SetEnabled;
using Rappix.Contracts.Merchants;

namespace Rappix.Catalog.Infrastructure.Messaging;

/// <summary>Habilita el catalogo cuando un merchant suspendido vuelve a estar activo.</summary>
internal sealed partial class MerchantActivatedConsumer(ISender sender, ILogger<MerchantActivatedConsumer> logger)
    : IConsumer<MerchantActivatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<MerchantActivatedIntegrationEvent> context)
    {
        Guid merchantId = context.Message.MerchantId;
        LogEnabling(logger, merchantId);

        Result result = await sender.Send(new SetCatalogEnabledCommand(merchantId, Enabled: true), context.CancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"No se pudo habilitar el catalogo del merchant {merchantId} (error: {result.Error.Code}).");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Habilitando el catalogo del merchant {MerchantId}.")]
    private static partial void LogEnabling(ILogger logger, Guid merchantId);
}
