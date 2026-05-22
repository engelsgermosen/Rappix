using System.Security.Claims;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.WebApi.Authentication;
using Rappix.BuildingBlocks.WebApi.Endpoints;
using Rappix.Pricing.Api.Contracts;
using Rappix.Pricing.Application.Quotes.Create;
using Rappix.Pricing.Application.Quotes.Get;
using Rappix.Pricing.Application.Responses;

namespace Rappix.Pricing.Api.Endpoints;

/// <summary>Endpoints de cotizacion para el cliente autenticado bajo /pricing (JWT userType=Customer).</summary>
internal static class PricingEndpoints
{
    public static RouteGroupBuilder MapPricingEndpoints(this RouteGroupBuilder group)
    {
        RouteGroupBuilder pricing = group.MapGroup("/pricing")
            .WithTags("Pricing")
            .RequireAuthorization("RequireCustomer");

        pricing.MapPost("/quotes", async (CreateQuoteRequest request, ClaimsPrincipal principal, ISender sender, CancellationToken cancellationToken) =>
        {
            Guid? userId = principal.GetUserId();
            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var command = new CreateQuoteCommand(
                userId.Value,
                request.MerchantId,
                request.Vertical,
                request.DistanceKm,
                request.ZoneId,
                request.Tip,
                request.CouponCode,
                request.IsFirstOrder,
                [.. request.Lines.Select(line => new QuoteLineInput(line.ItemId, line.Quantity, line.ModifierTotal))]);

            Result<QuoteResponse> result = await sender.Send(command, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/v1/pricing/quotes/{result.Value.QuoteId}", result.Value)
                : result.ToHttpResult();
        });

        pricing.MapGet("/quotes/{quoteId:guid}", async (Guid quoteId, ISender sender, CancellationToken cancellationToken) =>
            (await sender.Send(new GetQuoteQuery(quoteId), cancellationToken)).ToHttpResult());

        return group;
    }
}
