namespace Rappix.Merchants.Domain.Merchants;

/// <summary>Forma de una zona de cobertura: poligono custom o circulo (centro + radio).</summary>
public enum ServiceAreaType
{
    /// <summary>Poligono geometrico custom.</summary>
    Polygon = 0,

    /// <summary>Circulo definido por centro y radio en metros.</summary>
    Circle = 1,
}
