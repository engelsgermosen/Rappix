using FluentValidation;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Items.Update;

/// <summary>Validacion del comando de actualizacion de item.</summary>
internal sealed class UpdateItemCommandValidator : AbstractValidator<UpdateItemCommand>
{
    public UpdateItemCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(Item.MaxNameLength);
        RuleFor(command => command.Description).MaximumLength(Item.MaxDescriptionLength);
        RuleFor(command => command.PriceAmount).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Currency)
            .Length(3)
            .When(command => !string.IsNullOrWhiteSpace(command.Currency));
    }
}
