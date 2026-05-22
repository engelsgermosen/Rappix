using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Responses;

namespace Rappix.Catalog.Application.Pricing.GetItemPricing;

/// <summary>Pricing/validacion de un item para otros servicios (via gRPC CatalogValidationService).</summary>
public sealed record GetItemPricingQuery(Guid ItemId) : IRequest<Result<ItemPricingResponse>>;
