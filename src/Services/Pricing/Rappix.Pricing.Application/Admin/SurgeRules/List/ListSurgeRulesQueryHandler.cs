using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Surge;

namespace Rappix.Pricing.Application.Admin.SurgeRules.List;

/// <summary>Lista las reglas de surge para administracion.</summary>
internal sealed class ListSurgeRulesQueryHandler(ISurgeRuleRepository surgeRules)
    : IRequestHandler<ListSurgeRulesQuery, Result<IReadOnlyList<SurgeRuleResponse>>>
{
    public async Task<Result<IReadOnlyList<SurgeRuleResponse>>> Handle(ListSurgeRulesQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<SurgeRule> rules = await surgeRules.ListAsync(cancellationToken);
        IReadOnlyList<SurgeRuleResponse> response = [.. rules.Select(SurgeRuleResponse.From)];
        return Result.Success(response);
    }
}
