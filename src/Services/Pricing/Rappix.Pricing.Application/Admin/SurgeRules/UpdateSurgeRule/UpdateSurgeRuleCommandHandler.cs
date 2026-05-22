using MediatR;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Application.Configuration;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Errors;
using Rappix.Pricing.Domain.Surge;

namespace Rappix.Pricing.Application.Admin.SurgeRules.UpdateSurgeRule;

/// <summary>Actualiza una regla de surge existente.</summary>
internal sealed class UpdateSurgeRuleCommandHandler(
    ISurgeRuleRepository surgeRules,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IOptions<PricingOptions> options)
    : IRequestHandler<UpdateSurgeRuleCommand, Result<SurgeRuleResponse>>
{
    public async Task<Result<SurgeRuleResponse>> Handle(UpdateSurgeRuleCommand command, CancellationToken cancellationToken)
    {
        SurgeRule? rule = await surgeRules.GetByIdAsync(new SurgeRuleId(command.SurgeRuleId), cancellationToken);
        if (rule is null)
        {
            return Result.Failure<SurgeRuleResponse>(SurgeErrors.NotFound);
        }

        Result update = rule.Update(
            command.ZoneId,
            command.Vertical,
            command.StartHour,
            command.EndHour,
            command.Multiplier,
            command.Priority,
            command.IsActive,
            options.Value.MaxSurgeMultiplier,
            clock.UtcNow);
        if (update.IsFailure)
        {
            return Result.Failure<SurgeRuleResponse>(update.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SurgeRuleResponse.From(rule);
    }
}
