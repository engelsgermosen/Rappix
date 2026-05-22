namespace Rappix.Pricing.Domain.Common;

/// <summary>Vertical de negocio del pedido (espejo del VerticalType de Merchants/Catalog).</summary>
public enum VerticalType
{
    /// <summary>Comida / restaurantes.</summary>
    Food = 0,

    /// <summary>Farmacia.</summary>
    Pharmacy = 1,

    /// <summary>Supermercado / colmado.</summary>
    Grocery = 2,

    /// <summary>Paqueteria / envios.</summary>
    Parcel = 3,
}
