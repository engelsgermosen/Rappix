using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Rappix.Merchants.Tests.Integration;

/// <summary>Datos de prueba reutilizables.</summary>
internal static class TestData
{
    /// <summary>RNC unico de 9 digitos (el RNC tiene indice unico global).</summary>
    public static string UniqueRnc() => Random.Shared.Next(100_000_000, 1_000_000_000).ToString();

    /// <summary>Genera los bytes de un PNG en memoria con las dimensiones dadas.</summary>
    public static byte[] PngBytes(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        using var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }
}
