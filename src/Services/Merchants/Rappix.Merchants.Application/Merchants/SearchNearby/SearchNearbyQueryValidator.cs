using FluentValidation;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Merchants.SearchNearby;

/// <summary>Validacion de la busqueda por cercania.</summary>
internal sealed class SearchNearbyQueryValidator : AbstractValidator<SearchNearbyQuery>
{
    public SearchNearbyQueryValidator()
    {
        RuleFor(query => query.Latitude).InclusiveBetween(-90, 90);
        RuleFor(query => query.Longitude).InclusiveBetween(-180, 180);
        RuleFor(query => query.Vertical)
            .Must(value => Enum.TryParse<VerticalType>(value, ignoreCase: true, out _))
            .When(query => query.Vertical is not null)
            .WithMessage("vertical invalido (Food, Pharmacy, Grocery o Parcel).");
    }
}
