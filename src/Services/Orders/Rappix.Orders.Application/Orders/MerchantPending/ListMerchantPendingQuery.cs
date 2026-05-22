using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Orders.Application.Responses;

namespace Rappix.Orders.Application.Orders.MerchantPending;

/// <summary>Lista los pedidos que esperan la aceptacion de un merchant (paginado).</summary>
public sealed record ListMerchantPendingQuery(Guid MerchantId, int Page, int PageSize) : IRequest<Result<IReadOnlyList<OrderResponse>>>;
