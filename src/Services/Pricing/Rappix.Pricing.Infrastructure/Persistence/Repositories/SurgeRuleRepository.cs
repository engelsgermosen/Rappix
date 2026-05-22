using Microsoft.EntityFrameworkCore;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Domain.Surge;

namespace Rappix.Pricing.Infrastructure.Persistence.Repositories;

/// <summary>Implementacion EF Core del repositorio del agregado <see cref="SurgeRule"/>.</summary>
internal sealed class SurgeRuleRepository(PricingDbContext context) : ISurgeRuleRepository
{
    public void Add(SurgeRule rule) => context.SurgeRules.Add(rule);

    public Task<SurgeRule?> GetByIdAsync(SurgeRuleId id, CancellationToken cancellationToken) =>
        context.SurgeRules.FirstOrDefaultAsync(rule => rule.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SurgeRule>> GetActiveAsync(CancellationToken cancellationToken) =>
        await context.SurgeRules.Where(rule => rule.IsActive).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SurgeRule>> ListAsync(CancellationToken cancellationToken) =>
        await context.SurgeRules.OrderByDescending(rule => rule.CreatedAtUtc).ToListAsync(cancellationToken);
}
