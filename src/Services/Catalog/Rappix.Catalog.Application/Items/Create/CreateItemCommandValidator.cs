using FluentValidation;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Items.Create;

/// <summary>Validacion del comando de alta de item.</summary>
internal sealed class CreateItemCommandValidator : AbstractValidator<CreateItemCommand>
{
    public CreateItemCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Item.MaxNameLength);
        RuleFor(command => command.Description).MaximumLength(Item.MaxDescriptionLength);
        RuleFor(command => command.PriceAmount).GreaterThanOrEqualTo(0);
        RuleFor(command => command.InitialStock).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Currency)
            .Length(3)
            .When(command => !string.IsNullOrWhiteSpace(command.Currency));
    }
}
