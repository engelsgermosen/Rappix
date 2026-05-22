using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Errors;
using Rappix.Pricing.Domain.Quotes;

namespace Rappix.Pricing.Application.Quotes.Consume;

/// <summary>
/// Consume una cotizacion y, si aplico un cupon, lo redime en la misma transaccion. El incremento de
/// UsedCount usa el token xmin del cupon: dos consumos concurrentes que compartan cupon chocan y uno
/// reintenta, evitando exceder MaxUses. Eleva el evento de redencion del cupon (se publica por el outbox).
/// </summary>
internal sealed class ConsumeQuoteCommandHandler(
    IQuoteRepository quotes,
    ICouponRepository coupons,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<ConsumeQuoteCommand, Result<QuoteResponse>>
{
    public async Task<Result<QuoteResponse>> Handle(ConsumeQuoteCommand command, CancellationToken cancellationToken)
    {
        DateTime now = clock.UtcNow;

        Quote? quote = await quotes.GetByIdAsync(new QuoteId(command.QuoteId), cancellationToken);
        if (quote is null)
        {
            return Result.Failure<QuoteResponse>(QuoteErrors.NotFound);
        }

        Result consume = quote.Consume(now);
        if (consume.IsFailure)
        {
            // Si el consumo fallo por expiracion, la cotizacion quedo marcada expirada: persistir la transicion.
            if (consume.Error.Code == QuoteErrors.Expired.Code)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result.Failure<QuoteResponse>(consume.Error);
        }

        if (quote.AppliedCouponId is { } couponId)
        {
            Coupon? coupon = await coupons.GetByIdIncludingDeletedAsync(couponId, cancellationToken);
            if (coupon is not null)
            {
                Result redeem = coupon.Redeem(quote.Id, quote.CustomerUserId, quote.Breakdown.DiscountAmount, now);
                if (redeem.IsFailure)
                {
                    return Result.Failure<QuoteResponse>(redeem.Error);
                }

                coupons.AddRedemption(CouponRedemption.Create(couponId, quote.CustomerUserId, quote.Id, now));
            }
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Failure<QuoteResponse>(QuoteErrors.ConcurrencyConflict);
        }

        return QuoteResponse.From(quote);
    }
}
