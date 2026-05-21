using MediatR;
using Rappix.BuildingBlocks.WebApi.Endpoints;
using Rappix.Merchants.Application.Merchants.GetById;
using Rappix.Merchants.Application.Merchants.GetBySlug;
using Rappix.Merchants.Application.Merchants.GetLogoUrl;
using Rappix.Merchants.Application.Merchants.SearchNearby;

namespace Rappix.Merchants.Api.Endpoints;

/// <summary>Endpoints publicos (sin autenticacion) de consulta de comercios bajo /merchants.</summary>
internal static class PublicMerchantEndpoints
{
    public static RouteGroupBuilder MapPublicMerchantEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder merchants = group.MapGroup("/merchants").WithTags("Merchants (public)");

        merchants.MapGet("/nearby", async (
            double lat,
            double lng,
            string? vertical,
            int? page,
            int? pageSize,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new SearchNearbyQuery(lat, lng, vertical, page ?? 1, pageSize ?? 20);
            return (await sender.Send(query, cancellationToken)).ToHttpResult();
        });

        merchants.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new GetMerchantByIdQuery(id), cancellationToken)).ToHttpResult());

        merchants.MapGet("/by-slug/{slug}", async (string slug, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new GetMerchantBySlugQuery(slug), cancellationToken)).ToHttpResult());

        merchants.MapGet("/{id:guid}/logo-url", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new GetLogoUrlQuery(id), cancellationToken)).ToHttpResult());

        return group;
    }
}
