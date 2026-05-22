using FluentValidation;

namespace Rappix.Orders.Application.Orders.Place;

/// <summary>Valida que haya cliente, cotizacion y una direccion de entrega con coordenadas en rango.</summary>
internal sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(command => command.CustomerUserId).NotEmpty();
        RuleFor(command => command.QuoteId).NotEmpty();
        RuleFor(command => command.Street).NotEmpty();
        RuleFor(command => command.Latitude).InclusiveBetween(-90d, 90d);
        RuleFor(command => command.Longitude).InclusiveBetween(-180d, 180d);
    }
}
