using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Responses;

namespace Rappix.Pricing.Application.Admin.SurgeRules.List;

/// <summary>Lista las reglas de surge no borradas (admin).</summary>
public sealed record ListSurgeRulesQuery : IRequest<Result<IReadOnlyList<SurgeRuleResponse>>>;
