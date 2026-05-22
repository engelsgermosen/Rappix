using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Catalog.Application.Stock.Release;

/// <summary>
/// Libera los holds de un pedido: las unidades apartadas vuelven al disponible (compensacion de la
/// saga). Idempotente: liberar un pedido sin holds activos (ya liberado) es un no-op exitoso.
/// </summary>
public sealed record ReleaseStockCommand(Guid OrderId) : IRequest<Result>;
