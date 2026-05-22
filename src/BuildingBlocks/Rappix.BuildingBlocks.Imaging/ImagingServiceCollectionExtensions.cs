using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Imaging;

namespace Rappix.BuildingBlocks.Imaging;

/// <summary>Registro del validador de imagenes basado en ImageSharp.</summary>
public static class ImagingServiceCollectionExtensions
{
    /// <summary>Registra IImageValidator (validacion por header con ImageSharp).</summary>
    public static IServiceCollection AddRappixImageValidator(this IServiceCollection services)
    {
        services.AddSingleton<IImageValidator, ImageSharpImageValidator>();
        return services;
    }
}
