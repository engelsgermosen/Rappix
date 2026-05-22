using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Orders.Application.Responses;

namespace Rappix.Orders.Application.Orders.List;

/// <summary>Lista los pedidos del cliente (paginado, mas recientes primero).</summary>
public sealed record ListOrdersQuery(Guid CustomerUserId, int Page, int PageSize) : IRequest<Result<IReadOnlyList<OrderResponse>>>;
