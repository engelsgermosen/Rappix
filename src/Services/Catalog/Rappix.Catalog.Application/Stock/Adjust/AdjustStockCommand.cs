using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Responses;

namespace Rappix.Catalog.Application.Stock.Adjust;

/// <summary>Modo de ajuste de stock.</summary>
public enum StockAdjustmentMode
{
    /// <summary>Fija el stock a un valor absoluto.</summary>
    Set = 0,

    /// <summary>Suma unidades al stock actual.</summary>
    Restock = 1,
}

/// <summary>Ajusta el stock de un item del merchant (correccion o reposicion).</summary>
public sealed record AdjustStockCommand(Guid MerchantId, Guid ItemId, StockAdjustmentMode Mode, int Quantity)
    : IRequest<Result<StockResponse>>;
