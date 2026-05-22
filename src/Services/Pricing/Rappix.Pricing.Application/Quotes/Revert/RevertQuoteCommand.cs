using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Responses;

namespace Rappix.Pricing.Application.Quotes.Revert;

/// <summary>
/// Revierte el consumo de una cotizacion (compensacion de la saga de Orders): devuelve la cotizacion a
/// Active y, si tenia cupon, deshace la redencion (decrementa UsedCount y marca la redencion como revertida,
/// conservando la auditoria). Solo valido si la consumio <see cref="OrderId"/>. Idempotente.
/// </summary>
public sealed record RevertQuoteCommand(Guid QuoteId, Guid OrderId, string? Reason) : IRequest<Result<QuoteResponse>>;
