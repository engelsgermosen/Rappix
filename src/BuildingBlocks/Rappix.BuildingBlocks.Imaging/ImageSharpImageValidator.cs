using Rappix.BuildingBlocks.Core.Imaging;
using Rappix.BuildingBlocks.Core.Results;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace Rappix.BuildingBlocks.Imaging;

/// <summary>
/// Valida una imagen leyendo solo el header (sin decodificarla completa): formato permitido
/// (PNG, JPEG o WebP), dimensiones entre 100x100 y 2000x2000, y relacion de aspecto no mayor a 5:1.
/// El tamano maximo se verifica antes, en el endpoint.
/// </summary>
public sealed class ImageSharpImageValidator : IImageValidator
{
    private const int MinDimension = 100;
    private const int MaxDimension = 2000;
    private const double MaxAspectRatio = 5d;
    private const double MinAspectRatio = 1d / MaxAspectRatio;

    public async Task<Result<string>> ValidateAsync(Stream imageStream, CancellationToken cancellationToken)
    {
        if (imageStream.CanSeek)
        {
            imageStream.Position = 0;
        }

        IImageFormat format;
        ImageInfo info;
        try
        {
            format = await Image.DetectFormatAsync(imageStream, cancellationToken);

            if (imageStream.CanSeek)
            {
                imageStream.Position = 0;
            }

            info = await Image.IdentifyAsync(imageStream, cancellationToken);
        }
        catch (UnknownImageFormatException)
        {
            return ImageValidationErrors.InvalidFormat;
        }

        if (format is not (PngFormat or JpegFormat or WebpFormat))
        {
            return ImageValidationErrors.InvalidFormat;
        }

        if (info.Width is < MinDimension or > MaxDimension || info.Height is < MinDimension or > MaxDimension)
        {
            return ImageValidationErrors.InvalidDimensions;
        }

        double ratio = (double)info.Width / info.Height;
        if (ratio is > MaxAspectRatio or < MinAspectRatio)
        {
            return ImageValidationErrors.ExtremeAspectRatio;
        }

        return format.DefaultMimeType;
    }
}
