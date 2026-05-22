using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Common;

namespace Rappix.Pricing.Application.Admin.SurgeRules.UpdateSurgeRule;

/// <summary>Actualiza una regla de surge (admin).</summary>
public sealed record UpdateSurgeRuleCommand(
    Guid SurgeRuleId,
    string? ZoneId,
    VerticalType? Vertical,
    int StartHour,
    int EndHour,
    decimal Multiplier,
    int Priority,
    bool IsActive) : IRequest<Result<SurgeRuleResponse>>;
