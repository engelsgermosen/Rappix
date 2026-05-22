using MediatR;
using Rappix.BuildingBlocks.WebApi.Endpoints;
using Rappix.Catalog.Application.Items.GetPhotoUrl;
using Rappix.Catalog.Application.Public.GetItem;
using Rappix.Catalog.Application.Public.Search;

namespace Rappix.Catalog.Api.Endpoints;

/// <summary>Endpoints publicos (sin autenticacion) de consulta del catalogo bajo /catalog.</summary>
internal static class PublicCatalogEndpoints
{
    public static RouteGroupBuilder MapPublicCatalogEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder catalog = group.MapGroup("/catalog").WithTags("Catalog (public)");

        catalog.MapGet("/items/search", async (
            string? q,
            Guid? merchantId,
            Guid? categoryId,
            int? page,
            int? pageSize,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var query = new SearchItemsQuery(q, merchantId, categoryId, page ?? 1, pageSize ?? 20);
            return (await sender.Send(query, cancellationToken)).ToHttpResult();
        });

        catalog.MapGet("/items/{itemId:guid}", async (Guid itemId, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new GetPublicItemQuery(itemId), cancellationToken)).ToHttpResult());

        catalog.MapGet("/items/{itemId:guid}/photo-url", async (Guid itemId, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new GetItemPhotoUrlQuery(itemId), cancellationToken)).ToHttpResult());

        return group;
    }
}
