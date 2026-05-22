using FluentAssertions;
using Rappix.BuildingBlocks.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Rappix.Merchants.Tests.Unit;

/// <summary>Pruebas del validador de logos. Las imagenes se generan en memoria con ImageSharp.</summary>
public sealed class ImageValidatorTests
{
    private readonly ImageSharpImageValidator _validator = new();

    [Fact]
    public async Task ValidPng_ReturnsPngContentType()
    {
        await using Stream image = CreateImage(400, 400, new PngEncoder());

        var result = await _validator.ValidateAsync(image, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("image/png");
    }

    [Fact]
    public async Task ValidJpeg_ReturnsJpegContentType()
    {
        await using Stream image = CreateImage(300, 300, new JpegEncoder());

        var result = await _validator.ValidateAsync(image, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task TooSmall_IsRejected()
    {
        await using Stream image = CreateImage(50, 50, new PngEncoder());

        var result = await _validator.ValidateAsync(image, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task TooLargeDimensions_IsRejected()
    {
        await using Stream image = CreateImage(2100, 300, new PngEncoder());

        var result = await _validator.ValidateAsync(image, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExtremeAspectRatio_IsRejected()
    {
        await using Stream image = CreateImage(1000, 100, new PngEncoder());

        var result = await _validator.ValidateAsync(image, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GifFormat_IsRejected()
    {
        await using Stream image = CreateImage(300, 300, new GifEncoder());

        var result = await _validator.ValidateAsync(image, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task NonImageBytes_AreRejected()
    {
        await using Stream garbage = new MemoryStream([0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07]);

        var result = await _validator.ValidateAsync(garbage, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    private static MemoryStream CreateImage(int width, int height, IImageEncoder encoder)
    {
        using var image = new Image<Rgba32>(width, height);
        var stream = new MemoryStream();
        image.Save(stream, encoder);
        stream.Position = 0;
        return stream;
    }
}
