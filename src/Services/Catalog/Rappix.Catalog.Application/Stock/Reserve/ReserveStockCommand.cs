using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Catalog.Application.Stock.Reserve;

/// <summary>Una linea de la reserva: cuantas unidades de un item apartar.</summary>
public sealed record ReserveStockLine(Guid ItemId, int Quantity);

/// <summary>
/// Aparta (hold) stock para un pedido, todo-o-nada sobre sus lineas. Idempotente por OrderId: una
/// re-entrega del mismo pedido no duplica holds. El hold expira a los <see cref="TtlSeconds"/> segundos
/// (TTL) para que un pedido abandonado no deje stock colgado. No descuenta el fisico hasta el commit.
/// </summary>
public sealed record ReserveStockCommand(Guid OrderId, IReadOnlyList<ReserveStockLine> Lines, int TtlSeconds)
    : IRequest<Result>;
