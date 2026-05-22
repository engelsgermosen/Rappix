using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Responses;

namespace Rappix.Pricing.Application.Admin.Coupons.Get;

/// <summary>Obtiene un cupon por id (admin).</summary>
public sealed record GetCouponQuery(Guid CouponId) : IRequest<Result<CouponResponse>>;
