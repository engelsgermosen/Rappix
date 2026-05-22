using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Errors;
using Rappix.Pricing.Domain.Quotes;

namespace Rappix.Pricing.Application.Quotes.Get;

/// <summary>
/// Recupera una cotizacion. Expiracion perezosa: si esta vigente pero vencio por tiempo, la marca expirada
/// (y persiste la transicion) y devuelve <see cref="QuoteErrors.Expired"/>. El cleanup batch queda como
/// hook futuro documentado (ADR-0005).
/// </summary>
internal sealed class GetQuoteQueryHandler(
    IQuoteRepository quotes,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<GetQuoteQuery, Result<QuoteResponse>>
{
    public async Task<Result<QuoteResponse>> Handle(GetQuoteQuery query, CancellationToken cancellationToken)
    {
        Quote? quote = await quotes.GetByIdAsync(new QuoteId(query.QuoteId), cancellationToken);
        if (quote is null)
        {
            return Result.Failure<QuoteResponse>(QuoteErrors.NotFound);
        }

        if (quote.IsExpiredAt(clock.UtcNow))
        {
            quote.MarkExpired();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<QuoteResponse>(QuoteErrors.Expired);
        }

        return quote.Status == QuoteStatus.Expired
            ? Result.Failure<QuoteResponse>(QuoteErrors.Expired)
            : QuoteResponse.From(quote);
    }
}
