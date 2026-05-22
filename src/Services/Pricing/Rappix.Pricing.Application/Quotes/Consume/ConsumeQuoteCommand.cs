using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Responses;

namespace Rappix.Pricing.Application.Quotes.Consume;

/// <summary>
/// Consume una cotizacion (la referencia un pedido). Es la operacion que Orders invoca por gRPC
/// (MarkCouponUsed): marca la cotizacion como Consumed y, si tenia cupon, lo redime (incrementa UsedCount
/// con concurrencia optimista y registra la redencion del usuario).
/// </summary>
public sealed record ConsumeQuoteCommand(Guid QuoteId) : IRequest<Result<QuoteResponse>>;
