namespace Rappix.Catalog.Domain.Catalogs;

/// <summary>Vertical de negocio del catalogo (espejo del VerticalType de Merchants; se cachea desde el evento).</summary>
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
