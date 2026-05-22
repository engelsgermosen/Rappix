using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Common;

namespace Rappix.Pricing.Application.Admin.SurgeRules.CreateSurgeRule;

/// <summary>Crea una regla de surge (admin).</summary>
public sealed record CreateSurgeRuleCommand(
    string? ZoneId,
    VerticalType? Vertical,
    int StartHour,
    int EndHour,
    decimal Multiplier,
    int Priority) : IRequest<Result<SurgeRuleResponse>>;
