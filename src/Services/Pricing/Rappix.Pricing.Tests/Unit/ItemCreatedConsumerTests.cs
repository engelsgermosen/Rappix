using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.ItemPrices.Cache;
using Rappix.Pricing.Infrastructure.Messaging;
using Rappix.Contracts.Catalog;

namespace Rappix.Pricing.Tests.Unit;

/// <summary>Prueba que el consumer de ItemCreated traduce el evento al comando de cache con los campos correctos.</summary>
public sealed class ItemCreatedConsumerTests
{
    [Fact]
    public async Task Consume_ForwardsCacheItemPriceCommand()
    {
        ISender sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<CacheItemPriceCommand>(), Arg.Any<CancellationToken>()).Returns(Result.Success());

        var consumer = new ItemCreatedConsumer(sender, NullLogger<ItemCreatedConsumer>.Instance);

        var message = new ItemCreatedIntegrationEvent
        {
            MerchantId = Guid.CreateVersion7(),
            ItemId = Guid.CreateVersion7(),
            Name = "Pizza Pepperoni",
            BasePriceAmount = 450m,
            Currency = "DOP",
        };
        ConsumeContext<ItemCreatedIntegrationEvent> context = Substitute.For<ConsumeContext<ItemCreatedIntegrationEvent>>();
        context.Message.Returns(message);

        await consumer.Consume(context);

        await sender.Received(1).Send(
            Arg.Is<CacheItemPriceCommand>(command =>
                command.ItemId == message.ItemId
                && command.MerchantId == message.MerchantId
                && command.Name == "Pizza Pepperoni"
                && command.BasePrice == 450m
                && command.Currency == "DOP"),
            Arg.Any<CancellationToken>());
    }
}
