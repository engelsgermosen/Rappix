using System.Security.Claims;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.WebApi.Authentication;
using Rappix.BuildingBlocks.WebApi.Endpoints;
using Rappix.Catalog.Api.Contracts;
using Rappix.Catalog.Application.Catalogs.AddCategory;
using Rappix.Catalog.Application.Catalogs.GetMine;
using Rappix.Catalog.Application.Catalogs.RemoveCategory;
using Rappix.Catalog.Application.Catalogs.ResolveMerchant;
using Rappix.Catalog.Application.Items.AddModifier;
using Rappix.Catalog.Application.Items.AddModifierOption;
using Rappix.Catalog.Application.Items.Create;
using Rappix.Catalog.Application.Items.Delete;
using Rappix.Catalog.Application.Items.GetMine;
using Rappix.Catalog.Application.Items.ListMine;
using Rappix.Catalog.Application.Items.SetAttributes;
using Rappix.Catalog.Application.Items.SetAvailability;
using Rappix.Catalog.Application.Items.Update;
using Rappix.Catalog.Application.Items.UploadPhoto;
using Rappix.Catalog.Application.Stock.Adjust;

namespace Rappix.Catalog.Api.Endpoints;

/// <summary>
/// Endpoints del catalogo del merchant autenticado bajo /catalog/me. Cada uno resuelve el MerchantId
/// del usuario (sujeto JWT) antes de operar, via <see cref="ResolveMerchantIdQuery"/>.
/// </summary>
internal static class CatalogOwnerEndpoints
{
    private const long MaxPhotoBytes = 4 * 1024 * 1024;

    public static RouteGroupBuilder MapCatalogOwnerEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder owner = group.MapGroup("/catalog/me")
            .WithTags("Catalog (owner)")
            .RequireAuthorization("RequireMerchant");

        owner.MapGet("/", (ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
            ForOwnerAsync(principal, sender, async merchantId =>
                (await sender.Send(new GetMyCatalogQuery(merchantId), cancellationToken)).ToHttpResult(), cancellationToken));

        owner.MapPost("/categories", (CreateCategoryRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
            ForOwnerAsync(principal, sender, async merchantId =>
                (await sender.Send(new AddCategoryCommand(merchantId, request.Name, request.SortOrder), cancellationToken)).ToHttpResult(), cancellationToken));

        owner.MapDelete("/categories/{categoryId:guid}", (Guid categoryId, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
            ForOwnerAsync(principal, sender, async merchantId =>
                (await sender.Send(new RemoveCategoryCommand(merchantId, categoryId), cancellationToken)).ToHttpResult(), cancellationToken));

        owner.MapPost("/items", (CreateItemRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
            ForOwnerAsync(principal, sender, async merchantId =>
            {
                var command = new CreateItemCommand(
                    merchantId, request.CategoryId, request.Name, request.Description,
                    request.PriceAmount, request.Currency, request.TracksInventory, request.InitialStock, request.Attributes);
                return (await sender.Send(command, cancellationToken)).ToHttpResult();
            }, cancellationToken));

        owner.MapGet("/items", (int? page, int? pageSize, Guid? categoryId, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
            ForOwnerAsync(principal, sender, async merchantId =>
                (await sender.Send(new ListMyItemsQuery(merchantId, categoryId, page ?? 1, pageSize ?? 20), cancellationToken)).ToHttpResult(), cancellationToken));

        owner.MapGet("/items/{itemId:guid}", (Guid itemId, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
            ForOwnerAsync(principal, sender, async merchantId =>
                (await sender.Send(new GetMyItemQuery(merchantId, itemId), cancellationToken)).ToHttpResult(), cancellationToken));

        owner.MapPut("/items/{itemId:guid}", (Guid itemId, UpdateItemRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
            ForOwnerAsync(principal, sender, async merchantId =>
            {
                var command = new UpdateItemCommand(
                    merchantId, itemId, request.CategoryId, request.Name, request.Description, request.PriceAmount, request.Currency);
                return (await sender.Send(command, cancellationToken)).ToHttpResult();
            }, cancellationToken));

        owner.MapDelete("/items/{itemId:guid}", (Guid itemId, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
            ForOwnerAsync(principal, sender, async merchantId =>
                (await sender.Send(new DeleteItemCommand(merchantId, itemId), cancellationToken)).ToHttpResult(), cancellationToken));

        owner.MapPut("/items/{itemId:guid}/availability", (Guid itemId, SetAvailabilityRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
            ForOwnerAsync(principal, sender, async merchantId =>
                (await sender.Send(new SetItemAvailabilityCommand(merchantId, itemId, request.Available), cancellationToken)).ToHttpResult(), cancellationToken));

        owner.MapPut("/items/{itemId:guid}/attributes", (Guid itemId, SetItemAttributesRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
            ForOwnerAsync(principal, sender, async merchantId =>
                (await sender.Send(new SetItemAttributesCommand(merchantId, itemId, request.Attributes), cancellationToken)).ToHttpResult(), cancellationToken));

        owner.MapPost("/items/{itemId:guid}/modifiers", (Guid itemId, AddModifierRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
            ForOwnerAsync(principal, sender, async merchantId =>
            {
                var command = new AddModifierCommand(merchantId, itemId, request.Name, request.IsRequired, request.MinSelections, request.MaxSelections);
                return (await sender.Send(command, cancellationToken)).ToHttpResult();
            }, cancellationToken));

        owner.MapPost("/items/{itemId:guid}/modifiers/{modifierId:guid}/options", (Guid itemId, Guid modifierId, AddModifierOptionRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
            ForOwnerAsync(principal, sender, async merchantId =>
                (await sender.Send(new AddModifierOptionCommand(merchantId, itemId, modifierId, request.Name, request.PriceDelta), cancellationToken)).ToHttpResult(), cancellationToken));

        owner.MapPut("/items/{itemId:guid}/stock", (Guid itemId, AdjustStockRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
            ForOwnerAsync(principal, sender, async merchantId =>
            {
                if (!Enum.TryParse(request.Mode, ignoreCase: true, out StockAdjustmentMode mode))
                {
                    return Results.Problem(detail: "Mode invalido (Set o Restock).", statusCode: StatusCodes.Status400BadRequest);
                }

                return (await sender.Send(new AdjustStockCommand(merchantId, itemId, mode, request.Quantity), cancellationToken)).ToHttpResult();
            }, cancellationToken));

        owner.MapPost("/items/{itemId:guid}/photo", (Guid itemId, IFormFile file, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
            ForOwnerAsync(principal, sender, async merchantId =>
            {
                if (file is null || file.Length == 0)
                {
                    return Results.Problem(detail: "El archivo de imagen esta vacio.", statusCode: StatusCodes.Status400BadRequest);
                }

                if (file.Length > MaxPhotoBytes)
                {
                    return Results.Problem(detail: "La foto supera el tamano maximo de 4MB.", statusCode: StatusCodes.Status400BadRequest);
                }

                // Se bufferiza para garantizar un stream con seek (el validador y MinIO reposicionan el stream).
                await using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer, cancellationToken);
                buffer.Position = 0;

                return (await sender.Send(new UploadItemPhotoCommand(merchantId, itemId, buffer), cancellationToken)).ToHttpResult();
            }, cancellationToken)).DisableAntiforgery();

        return group;
    }

    private static async Task<IResult> ForOwnerAsync(
        ClaimsPrincipal principal,
        ISender sender,
        Func<Guid, Task<IResult>> operation,
        CancellationToken cancellationToken)
    {
        Guid? userId = principal.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        Result<Guid> merchant = await sender.Send(new ResolveMerchantIdQuery(userId.Value), cancellationToken);
        return merchant.IsFailure
            ? merchant.ToHttpResult()
            : await operation(merchant.Value);
    }
}
