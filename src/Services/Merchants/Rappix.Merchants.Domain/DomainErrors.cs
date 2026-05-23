using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Domain;

/// <summary>Errores de dominio del merchant.</summary>
public static class MerchantErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Merchants.Merchant.NotFound", "Comercio no encontrado.");

    public static readonly Error AlreadyExistsForOwner =
        Error.Conflict("Merchants.Merchant.AlreadyExistsForOwner", "El usuario ya tiene un comercio.");

    public static readonly Error IncompleteForSubmission =
        Error.Validation("Merchants.Merchant.IncompleteForSubmission", "Faltan datos para enviar a aprobacion: RNC, al menos una zona de cobertura, horarios y ubicacion de pickup.");

    public static readonly Error InvalidPickupLocation =
        Error.Validation("Merchants.Merchant.InvalidPickupLocation", "La ubicacion de pickup no es valida.");

    public static readonly Error ServiceAreaNotFound =
        Error.NotFound("Merchants.Merchant.ServiceAreaNotFound", "Zona de cobertura no encontrada.");

    public static Error SlugInUse(string slug) =>
        Error.Conflict("Merchants.Merchant.SlugInUse", $"El slug '{slug}' ya esta en uso.");

    public static Error RncInUse(string rnc) =>
        Error.Conflict("Merchants.Merchant.RncInUse", $"El RNC '{rnc}' ya esta registrado.");

    public static Error InvalidTransition(MerchantStatus from, string action) =>
        Error.Conflict("Merchants.Merchant.InvalidTransition", $"No se puede {action} un comercio en estado {from}.");
}

/// <summary>Errores del value object Slug.</summary>
public static class SlugErrors
{
    public static readonly Error Empty = Error.Validation("Merchants.Slug.Empty", "El slug es obligatorio.");
    public static readonly Error Invalid = Error.Validation("Merchants.Slug.Invalid", "El slug solo admite minusculas, numeros y guiones (3-60 caracteres).");
}

/// <summary>Errores del value object Rnc.</summary>
public static class RncErrors
{
    public static readonly Error Empty = Error.Validation("Merchants.Rnc.Empty", "El RNC es obligatorio.");
    public static readonly Error Invalid = Error.Validation("Merchants.Rnc.Invalid", "El RNC debe tener 9 digitos (empresa) u 11 (cedula).");
}

/// <summary>Errores del value object CommissionPercentage.</summary>
public static class CommissionErrors
{
    public static readonly Error OutOfRange = Error.Validation("Merchants.Commission.OutOfRange", "La comision debe estar entre 0 y 100.");
}

/// <summary>Errores de las zonas de cobertura.</summary>
public static class ServiceAreaErrors
{
    public static readonly Error InvalidPolygon = Error.Validation("Merchants.ServiceArea.InvalidPolygon", "El poligono no es valido.");
    public static readonly Error InvalidCenter = Error.Validation("Merchants.ServiceArea.InvalidCenter", "El centro del circulo no es valido.");
    public static readonly Error InvalidRadius = Error.Validation("Merchants.ServiceArea.InvalidRadius", "El radio debe estar entre 100 y 50000 metros.");
    public static readonly Error WrongSrid = Error.Validation("Merchants.ServiceArea.WrongSrid", "La geometria debe usar SRID 4326.");
}

/// <summary>Errores de horarios.</summary>
public static class OperatingHoursErrors
{
    public static readonly Error InvalidRange = Error.Validation("Merchants.OperatingHours.InvalidRange", "La hora de apertura debe ser anterior a la de cierre.");
}