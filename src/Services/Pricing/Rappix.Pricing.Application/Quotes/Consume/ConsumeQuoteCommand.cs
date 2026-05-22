using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Responses;

namespace Rappix.Pricing.Application.Quotes.Consume;

/// <summary>
/// Consume una cotizacion (la referencia un pedido). Es la operacion que Orders invoca por gRPC: marca la
/// cotizacion como Consumed por <see cref="OrderId"/> y, si tenia cupon, lo redime (incrementa UsedCount con
/// concurrencia optimista y registra la redencion). Idempotente por pedido: reintentar el mismo OrderId no
/// re-redime el cupon.
/// </summary>
public sealed record ConsumeQuoteCommand(Guid QuoteId, Guid OrderId) : IRequest<Result<QuoteResponse>>;
