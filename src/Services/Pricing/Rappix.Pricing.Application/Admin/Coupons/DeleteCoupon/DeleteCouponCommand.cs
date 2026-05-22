using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Pricing.Application.Admin.Coupons.DeleteCoupon;

/// <summary>Desactiva (borrado logico) un cupon (admin).</summary>
public sealed record DeleteCouponCommand(Guid CouponId) : IRequest<Result>;
