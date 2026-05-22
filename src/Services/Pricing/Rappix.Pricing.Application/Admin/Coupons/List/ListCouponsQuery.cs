using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Responses;

namespace Rappix.Pricing.Application.Admin.Coupons.List;

/// <summary>Lista los cupones no borrados (admin).</summary>
public sealed record ListCouponsQuery : IRequest<Result<IReadOnlyList<CouponResponse>>>;
