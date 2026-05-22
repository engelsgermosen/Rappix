using FluentValidation;

namespace Rappix.Pricing.Application.Quotes.Create;

/// <summary>Validacion del comando de cotizacion.</summary>
internal sealed class CreateQuoteCommandValidator : AbstractValidator<CreateQuoteCommand>
{
    public CreateQuoteCommandValidator()
    {
        RuleFor(command => command.CustomerUserId).NotEmpty();
        RuleFor(command => command.MerchantId).NotEmpty();
        RuleFor(command => command.DistanceKm).GreaterThanOrEqualTo(0m);
        RuleFor(command => command.Tip).GreaterThanOrEqualTo(0m);
        RuleFor(command => command.Lines).NotEmpty();
        RuleForEach(command => command.Lines).ChildRules(line =>
        {
            line.RuleFor(item => item.ItemId).NotEmpty();
            line.RuleFor(item => item.Quantity).GreaterThan(0);
            line.RuleFor(item => item.ModifierTotal).GreaterThanOrEqualTo(0m);
        });
    }
}
