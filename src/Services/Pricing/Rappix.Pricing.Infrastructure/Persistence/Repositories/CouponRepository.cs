using Microsoft.EntityFrameworkCore;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Domain.Coupons;

namespace Rappix.Pricing.Infrastructure.Persistence.Repositories;

/// <summary>Implementacion EF Core del repositorio del agregado <see cref="Coupon"/> y sus redenciones.</summary>
internal sealed class CouponRepository(PricingDbContext context) : ICouponRepository
{
    public void Add(Coupon coupon) => context.Coupons.Add(coupon);

    public Task<Coupon?> GetByIdAsync(CouponId id, CancellationToken cancellationToken) =>
        context.Coupons.FirstOrDefaultAsync(coupon => coupon.Id == id, cancellationToken);

    public Task<Coupon?> GetByIdIncludingDeletedAsync(CouponId id, CancellationToken cancellationToken) =>
        context.Coupons.IgnoreQueryFilters().FirstOrDefaultAsync(coupon => coupon.Id == id, cancellationToken);

    public Task<Coupon?> GetActiveByCodeAsync(string code, CancellationToken cancellationToken) =>
        context.Coupons.FirstOrDefaultAsync(
            coupon => coupon.Code == CouponCode.FromTrusted(code) && coupon.IsActive, cancellationToken);

    public Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken) =>
        context.Coupons.IgnoreQueryFilters().AnyAsync(coupon => coupon.Code == CouponCode.FromTrusted(code), cancellationToken);

    public async Task<IReadOnlyList<Coupon>> ListAsync(CancellationToken cancellationToken) =>
        await context.Coupons.OrderByDescending(coupon => coupon.CreatedAtUtc).ToListAsync(cancellationToken);

    public Task<int> CountRedemptionsAsync(CouponId couponId, Guid customerUserId, CancellationToken cancellationToken) =>
        context.CouponRedemptions.CountAsync(
            redemption => redemption.CouponId == couponId && redemption.CustomerUserId == customerUserId, cancellationToken);

    public void AddRedemption(CouponRedemption redemption) => context.CouponRedemptions.Add(redemption);
}
