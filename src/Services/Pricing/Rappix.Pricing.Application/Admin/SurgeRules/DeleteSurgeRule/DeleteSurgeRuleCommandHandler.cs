using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Domain.Errors;
using Rappix.Pricing.Domain.Surge;

namespace Rappix.Pricing.Application.Admin.SurgeRules.DeleteSurgeRule;

/// <summary>Desactiva una regla de surge (borrado logico).</summary>
internal sealed class DeleteSurgeRuleCommandHandler(
    ISurgeRuleRepository surgeRules,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<DeleteSurgeRuleCommand, Result>
{
    public async Task<Result> Handle(DeleteSurgeRuleCommand command, CancellationToken cancellationToken)
    {
        SurgeRule? rule = await surgeRules.GetByIdAsync(new SurgeRuleId(command.SurgeRuleId), cancellationToken);
        if (rule is null)
        {
            return Result.Failure(SurgeErrors.NotFound);
        }

        rule.Deactivate(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
