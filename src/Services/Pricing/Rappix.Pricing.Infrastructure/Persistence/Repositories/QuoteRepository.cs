using Microsoft.EntityFrameworkCore;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Domain.Quotes;

namespace Rappix.Pricing.Infrastructure.Persistence.Repositories;

/// <summary>Implementacion EF Core del repositorio del agregado <see cref="Quote"/>.</summary>
internal sealed class QuoteRepository(PricingDbContext context) : IQuoteRepository
{
    public void Add(Quote quote) => context.Quotes.Add(quote);

    public Task<Quote?> GetByIdAsync(QuoteId id, CancellationToken cancellationToken) =>
        context.Quotes.Include(quote => quote.Lines).FirstOrDefaultAsync(quote => quote.Id == id, cancellationToken);
}
