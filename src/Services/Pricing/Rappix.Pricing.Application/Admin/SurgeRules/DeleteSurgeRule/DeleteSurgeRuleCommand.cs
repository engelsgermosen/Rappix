using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Pricing.Application.Admin.SurgeRules.DeleteSurgeRule;

/// <summary>Desactiva (borrado logico) una regla de surge (admin).</summary>
public sealed record DeleteSurgeRuleCommand(Guid SurgeRuleId) : IRequest<Result>;
