using FluentValidation;
using Rappix.Pricing.Domain.Surge;

namespace Rappix.Pricing.Application.Admin.SurgeRules.CreateSurgeRule;

/// <summary>Validacion del comando de alta de regla de surge.</summary>
internal sealed class CreateSurgeRuleCommandValidator : AbstractValidator<CreateSurgeRuleCommand>
{
    public CreateSurgeRuleCommandValidator()
    {
        RuleFor(command => command.StartHour).InclusiveBetween(SurgeRule.MinHour, SurgeRule.MaxHour);
        RuleFor(command => command.EndHour).InclusiveBetween(SurgeRule.MinHour, SurgeRule.MaxHour);
        RuleFor(command => command.EndHour).GreaterThan(command => command.StartHour);
        RuleFor(command => command.Multiplier).GreaterThanOrEqualTo(1.0m);
        RuleFor(command => command.Priority).GreaterThanOrEqualTo(0);
    }
}
