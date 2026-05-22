using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.ItemPrices.Cache;
using Rappix.Contracts.Catalog;

namespace Rappix.Pricing.Infrastructure.Messaging;

/// <summary>
/// Cachea el precio base de un item cuando Catalog publica ItemCreated. El cache acelera la cotizacion y
/// sirve de fallback si el gRPC a Catalog no responde. Idempotente (upsert por ItemId); el inbox EF de
/// MassTransit garantiza el procesamiento once-only por mensaje.
/// </summary>
internal sealed partial class ItemCreatedConsumer(ISender sender, ILogger<ItemCreatedConsumer> logger)
    : IConsumer<ItemCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<ItemCreatedIntegrationEvent> context)
    {
        ItemCreatedIntegrationEvent message = context.Message;
        LogCaching(logger, message.ItemId, message.MerchantId);

        var command = new CacheItemPriceCommand(
            message.ItemId, message.MerchantId, message.Name, message.BasePriceAmount, message.Currency);
        Result result = await sender.Send(command, context.CancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"No se pudo cachear el precio del item {message.ItemId} (error: {result.Error.Code}).");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cacheando precio del item {ItemId} del merchant {MerchantId}.")]
    private static partial void LogCaching(ILogger logger, Guid itemId, Guid merchantId);
}
