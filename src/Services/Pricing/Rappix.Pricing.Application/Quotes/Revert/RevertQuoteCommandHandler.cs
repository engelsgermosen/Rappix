using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Errors;
using Rappix.Pricing.Domain.Quotes;

namespace Rappix.Pricing.Application.Quotes.Revert;

/// <summary>
/// Revierte el consumo de una cotizacion y deshace la redencion de su cupon (si lo tuvo), en una sola
/// transaccion. Idempotente: si la cotizacion ya volvio a Active y la redencion ya fue revertida, no hace
/// cambios. El xmin del cupon traduce conflictos concurrentes a Conflict.
/// </summary>
internal sealed class RevertQuoteCommandHandler(
    IQuoteRepository quotes,
    ICouponRepository coupons,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<RevertQuoteCommand, Result<QuoteResponse>>
{
    public async Task<Result<QuoteResponse>> Handle(RevertQuoteCommand command, CancellationToken cancellationToken)
    {
        DateTime now = clock.UtcNow;

        Quote? quote = await quotes.GetByIdAsync(new QuoteId(command.QuoteId), cancellationToken);
        if (quote is null)
        {
            return Result.Failure<QuoteResponse>(QuoteErrors.NotFound);
        }

        Result revert = quote.Revert(command.OrderId, now);
        if (revert.IsFailure)
        {
            return Result.Failure<QuoteResponse>(revert.Error);
        }

        // Deshacer la redencion del cupon, si existe una activa (no revertida) para esta cotizacion.
        if (quote.AppliedCouponId is { } couponId)
        {
            CouponRedemption? redemption = await coupons.GetActiveRedemptionAsync(couponId, quote.Id, cancellationToken);
            if (redemption is not null)
            {
                Coupon? coupon = await coupons.GetByIdIncludingDeletedAsync(couponId, cancellationToken);
                coupon?.UnRedeem(now);
                redemption.MarkReverted(command.Reason ?? "Reversion de la saga de Orders", now);
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
