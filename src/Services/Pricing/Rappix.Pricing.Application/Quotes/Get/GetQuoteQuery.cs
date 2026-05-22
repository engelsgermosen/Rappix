using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Responses;

namespace Rappix.Pricing.Application.Quotes.Get;

/// <summary>Recupera una cotizacion por id (con expiracion perezosa: si vencio, se marca expirada y falla).</summary>
public sealed record GetQuoteQuery(Guid QuoteId) : IRequest<Result<QuoteResponse>>;
