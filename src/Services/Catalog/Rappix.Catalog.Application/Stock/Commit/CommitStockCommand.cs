using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Catalog.Application.Stock.Commit;

/// <summary>
/// Confirma los holds de un pedido: descuenta del stock fisico las unidades apartadas (decremento
/// definitivo). Idempotente: confirmar un pedido sin holds activos (ya confirmado) es un no-op exitoso.
/// </summary>
public sealed record CommitStockCommand(Guid OrderId) : IRequest<Result>;
