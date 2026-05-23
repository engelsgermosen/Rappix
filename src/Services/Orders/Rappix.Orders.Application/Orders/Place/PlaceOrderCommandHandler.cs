using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Application.Responses;
using Rappix.Orders.Domain.Common;
using Rappix.Orders.Domain.Errors;
using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Application.Orders.Place;

/// <summary>
/// Crea el pedido: toma el snapshot del quote desde Pricing (gRPC), valida que sea usable y del mismo
/// cliente, congela lineas y desglose en el agregado Order y lo persiste. La creacion eleva
/// OrderSubmittedDomainEvent que (via outbox) arranca la saga.
/// </summary>
internal sealed class PlaceOrderCommandHandler(
    IOrderRepository orders,
    IPricingClient pricing,
    IMerchantValidationClient merchants,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<PlaceOrderCommand, Result<OrderResponse>>
{
    public async Task<Result<OrderResponse>> Handle(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        Result<DeliveryAddress> address = DeliveryAddress.Create(command.Street, command.Reference, command.Latitude, command.Longitude);
        if (address.IsFailure)
        {
            return Result.Failure<OrderResponse>(address.Error);
        }

        QuoteSnapshot quote = await pricing.GetQuoteAsync(command.QuoteId, cancellationToken);
        if (!quote.ServiceAvailable)
        {
            return Result.Failure<OrderResponse>(OrderErrors.QuoteNotUsable);
        }

        if (!quote.Found || !string.Equals(quote.Status, "Active", StringComparison.OrdinalIgnoreCase) || quote.CustomerUserId != command.CustomerUserId)
        {
            return Result.Failure<OrderResponse>(OrderErrors.QuoteNotUsable);
        }

        // Resolver el dueno del merchant para persistirlo en el pedido (autoriza accept/reject del merchant).
        // Tambien congela el pickup (ubicacion fisica) para que la saga lo propague a Dispatch en
        // CourierRequestedIntegrationEvent (Fase 6). Sin pickup el pedido no es enviable.
        MerchantInfo merchant = await merchants.GetAsync(quote.MerchantId, cancellationToken);
        if (!merchant.ServiceAvailable || !merchant.Found)
        {
            return Result.Failure<OrderResponse>(OrderErrors.MerchantUnavailable);
        }

        if (!merchant.HasPickupLocation)
        {
            return Result.Failure<OrderResponse>(OrderErrors.MerchantPickupMissing);
        }

        var lines = new List<OrderLine>(quote.Lines.Count);
        foreach (QuoteSnapshotLine line in quote.Lines)
        {
            Result<OrderLine> orderLine = OrderLine.Create(line.ItemId, line.ItemName, line.UnitPrice, line.ModifierTotal, line.Quantity);
            if (orderLine.IsFailure)
            {
                return Result.Failure<OrderResponse>(orderLine.Error);
            }

            lines.Add(orderLine.Value);
        }

        Result<Order> order = Order.Create(
            command.CustomerUserId,
            quote.MerchantId,
            merchant.OwnerUserId,
            command.QuoteId,
            quote.Vertical,
            quote.Currency,
            lines,
            quote.Subtotal,
            quote.DeliveryFee,
            quote.ServiceFee,
            quote.Tax,
            quote.Tip,
            quote.DiscountAmount,
            quote.Total,
            address.Value,
            merchant.PickupLatitude,
            merchant.PickupLongitude,
            clock.UtcNow);
        if (order.IsFailure)
        {
            return Result.Failure<OrderResponse>(order.Error);
        }

        orders.Add(order.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return OrderResponse.From(order.Value);
    }
}
