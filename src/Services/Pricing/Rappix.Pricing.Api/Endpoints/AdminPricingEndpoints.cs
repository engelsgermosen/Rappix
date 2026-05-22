using MediatR;
using Rappix.BuildingBlocks.WebApi.Endpoints;
using Rappix.Pricing.Api.Contracts;
using Rappix.Pricing.Application.Admin.Coupons.CreateCoupon;
using Rappix.Pricing.Application.Admin.Coupons.DeleteCoupon;
using Rappix.Pricing.Application.Admin.Coupons.Get;
using Rappix.Pricing.Application.Admin.Coupons.List;
using Rappix.Pricing.Application.Admin.Coupons.UpdateCoupon;
using Rappix.Pricing.Application.Admin.SurgeRules.CreateSurgeRule;
using Rappix.Pricing.Application.Admin.SurgeRules.DeleteSurgeRule;
using Rappix.Pricing.Application.Admin.SurgeRules.List;
using Rappix.Pricing.Application.Admin.SurgeRules.UpdateSurgeRule;

namespace Rappix.Pricing.Api.Endpoints;

/// <summary>Endpoints de administracion de surge y cupones bajo /admin/pricing (JWT userType=Admin).</summary>
internal static class AdminPricingEndpoints
{
    public static RouteGroupBuilder MapAdminPricingEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder admin = group.MapGroup("/admin/pricing")
            .WithTags("Pricing (admin)")
            .RequireAuthorization("RequireAdmin");

        MapSurgeRules(admin);
        MapCoupons(admin);

        return group;
    }

    private static void MapSurgeRules(RouteGroupBuilder admin)
    {
        admin.MapGet("/surge-rules", async (ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new ListSurgeRulesQuery(), cancellationToken)).ToHttpResult());

        admin.MapPost("/surge-rules", async (CreateSurgeRuleRequest request, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(
                new CreateSurgeRuleCommand(request.ZoneId, request.Vertical, request.StartHour, request.EndHour, request.Multiplier, request.Priority),
                cancellationToken)).ToHttpResult());

        admin.MapPut("/surge-rules/{surgeRuleId:guid}", async (Guid surgeRuleId, UpdateSurgeRuleRequest request, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(
                new UpdateSurgeRuleCommand(surgeRuleId, request.ZoneId, request.Vertical, request.StartHour, request.EndHour, request.Multiplier, request.Priority, request.IsActive),
                cancellationToken)).ToHttpResult());

        admin.MapDelete("/surge-rules/{surgeRuleId:guid}", async (Guid surgeRuleId, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new DeleteSurgeRuleCommand(surgeRuleId), cancellationToken)).ToHttpResult());
    }

    private static void MapCoupons(RouteGroupBuilder admin)
    {
        admin.MapGet("/coupons", async (ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new ListCouponsQuery(), cancellationToken)).ToHttpResult());

        admin.MapGet("/coupons/{couponId:guid}", async (Guid couponId, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new GetCouponQuery(couponId), cancellationToken)).ToHttpResult());

        admin.MapPost("/coupons", async (CreateCouponRequest request, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(
                new CreateCouponCommand(request.Code, request.DiscountType, request.Value, request.MaxUses, request.MinOrderAmount, request.ValidFromUtc, request.ValidUntilUtc, request.PerUserLimit, request.MerchantId),
                cancellationToken)).ToHttpResult());

        admin.MapPut("/coupons/{couponId:guid}", async (Guid couponId, UpdateCouponRequest request, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(
                new UpdateCouponCommand(couponId, request.DiscountType, request.Value, request.MaxUses, request.MinOrderAmount, request.ValidFromUtc, request.ValidUntilUtc, request.PerUserLimit, request.MerchantId, request.IsActive),
                cancellationToken)).ToHttpResult());

        admin.MapDelete("/coupons/{couponId:guid}", async (Guid couponId, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new DeleteCouponCommand(couponId), cancellationToken)).ToHttpResult());
    }
}
