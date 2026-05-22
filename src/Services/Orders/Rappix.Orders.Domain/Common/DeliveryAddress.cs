using Rappix.BuildingBlocks.Core.Results;
using Rappix.Orders.Domain.Errors;

namespace Rappix.Orders.Domain.Common;

/// <summary>
/// Direccion de entrega del pedido con coordenadas. Value object embebido en el agregado Order (snapshot
/// inmutable del momento del pedido).
/// </summary>
public sealed record DeliveryAddress
{
    private DeliveryAddress(string street, string? reference, double latitude, double longitude)
    {
        Street = street;
        Reference = reference;
        Latitude = latitude;
        Longitude = longitude;
    }

    // Constructor sin parametros para EF Core (materializacion del owned type).
    private DeliveryAddress()
    {
    }

    /// <summary>Calle / linea de direccion.</summary>
    public string Street { get; private init; } = null!;

    /// <summary>Referencia o detalle opcional (apto, piso, punto de referencia).</summary>
    public string? Reference { get; private init; }

    /// <summary>Latitud (WGS84).</summary>
    public double Latitude { get; private init; }

    /// <summary>Longitud (WGS84).</summary>
    public double Longitude { get; private init; }

    /// <summary>Crea una direccion validando que haya calle y coordenadas en rango.</summary>
    public static Result<DeliveryAddress> Create(string? street, string? reference, double latitude, double longitude)
    {
        if (string.IsNullOrWhiteSpace(street))
        {
            return Result.Failure<DeliveryAddress>(DeliveryAddressErrors.StreetRequired);
        }

        if (latitude is < -90d or > 90d || longitude is < -180d or > 180d)
        {
            return Result.Failure<DeliveryAddress>(DeliveryAddressErrors.InvalidCoordinates);
        }

        string? trimmedReference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        return new DeliveryAddress(street.Trim(), trimmedReference, latitude, longitude);
    }
}
