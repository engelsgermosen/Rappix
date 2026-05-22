using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Responses;

namespace Rappix.Catalog.Application.Stock.Decrement;

/// <summary>
/// Decrementa el stock de un item (lo consumiran los pedidos). El token de concurrencia xmin evita
/// la sobreventa: ante decrementos concurrentes uno gana y el otro recibe ConcurrencyConflict.
/// </summary>
public sealed record DecrementStockCommand(Guid MerchantId, Guid ItemId, int Quantity) : IRequest<Result<StockResponse>>;
