using MediatR;
using Rappix.BuildingBlocks.WebApi.Endpoints;
using Rappix.Merchants.Api.Contracts;
using Rappix.Merchants.Application.Admin.Approve;
using Rappix.Merchants.Application.Admin.ListByStatus;
using Rappix.Merchants.Application.Admin.Reject;
using Rappix.Merchants.Application.Admin.Suspend;
using Rappix.Merchants.Application.Admin.Unsuspend;
using Rappix.Merchants.Application.Admin.UpdateCommission;

namespace Rappix.Merchants.Api.Endpoints;

/// <summary>Endpoints de administracion (workflow de aprobacion) bajo /admin/merchants.</summary>
internal static class AdminMerchantEndpoints
{
    public static RouteGroupBuilder MapAdminMerchantEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder admin = group.MapGroup("/admin")
            .WithTags("Merchants (admin)")
            .RequireAuthorization("RequireAdmin");

        admin.MapGet("/merchants", async (
            string? status,
            int? page,
            int? pageSize,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new ListMerchantsByStatusQuery(status ?? "Pending", page ?? 1, pageSize ?? 20);
            return (await sender.Send(query, cancellationToken)).ToHttpResult();
        });

        admin.MapPost("/merchants/{id:guid}/approve", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new ApproveMerchantCommand(id), cancellationToken)).ToHttpResult());

        admin.MapPost("/merchants/{id:guid}/reject", async (Guid id, RejectMerchantRequest request, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new RejectMerchantCommand(id, request.Reason), cancellationToken)).ToHttpResult());

        admin.MapPost("/merchants/{id:guid}/suspend", async (Guid id, SuspendMerchantRequest request, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new SuspendMerchantCommand(id, request.Reason), cancellationToken)).ToHttpResult());

        admin.MapPost("/merchants/{id:guid}/unsuspend", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new UnsuspendMerchantCommand(id), cancellationToken)).ToHttpResult());

        admin.MapPut("/merchants/{id:guid}/commission", async (Guid id, UpdateCommissionRequest request, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new UpdateCommissionCommand(id, request.CommissionPercentage), cancellationToken)).ToHttpResult());

        return group;
    }
}
