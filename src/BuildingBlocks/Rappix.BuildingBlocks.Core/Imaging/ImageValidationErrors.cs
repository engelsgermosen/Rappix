using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.BuildingBlocks.Core.Imaging;

/// <summary>Errores genericos de validacion de imagenes (reutilizables por cualquier servicio).</summary>
public static class ImageValidationErrors
{
    public static readonly Error InvalidFormat =
        Error.Validation("Imaging.InvalidFormat", "La imagen debe ser PNG, JPEG o WebP.");

    public static readonly Error InvalidDimensions =
        Error.Validation("Imaging.InvalidDimensions", "Las dimensiones de la imagen deben estar entre 100x100 y 2000x2000 pixeles.");

    public static readonly Error ExtremeAspectRatio =
        Error.Validation("Imaging.ExtremeAspectRatio", "La relacion de aspecto de la imagen es demasiado extrema (maximo 5:1).");
}
