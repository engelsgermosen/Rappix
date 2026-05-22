using MediatR;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Application.Configuration;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Surge;

namespace Rappix.Pricing.Application.Admin.SurgeRules.CreateSurgeRule;

/// <summary>Crea una regla de surge validando el rango horario y el multiplicador contra el cap configurado.</summary>
internal sealed class CreateSurgeRuleCommandHandler(
    ISurgeRuleRepository surgeRules,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IOptions<PricingOptions> options)
    : IRequestHandler<CreateSurgeRuleCommand, Result<SurgeRuleResponse>>
{
    public async Task<Result<SurgeRuleResponse>> Handle(CreateSurgeRuleCommand command, CancellationToken cancellationToken)
    {
        Result<SurgeRule> rule = SurgeRule.Create(
            command.ZoneId,
            command.Vertical,
            command.StartHour,
            command.EndHour,
            command.Multiplier,
            command.Priority,
            options.Value.MaxSurgeMultiplier,
            clock.UtcNow);
        if (rule.IsFailure)
        {
            return Result.Failure<SurgeRuleResponse>(rule.Error);
        }

        surgeRules.Add(rule.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return SurgeRuleResponse.From(rule.Value);
    }
}
