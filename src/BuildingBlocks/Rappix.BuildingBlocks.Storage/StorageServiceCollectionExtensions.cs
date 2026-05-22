using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using Rappix.BuildingBlocks.Core.Storage;

namespace Rappix.BuildingBlocks.Storage;

/// <summary>Registro del almacenamiento de objetos en MinIO.</summary>
public static class StorageServiceCollectionExtensions
{
    /// <summary>
    /// Vincula MinioOptions desde la seccion "Minio", registra el cliente de MinIO, IObjectStorage y
    /// el inicializador del bucket.
    /// </summary>
    public static IServiceCollection AddRappixObjectStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MinioOptions>(options => configuration.GetSection(MinioOptions.SectionName).Bind(options));

        services.AddSingleton<IMinioClient>(provider =>
        {
            MinioOptions options = provider.GetRequiredService<IOptions<MinioOptions>>().Value;
            return new MinioClient()
                .WithEndpoint(options.Endpoint)
                .WithCredentials(options.AccessKey, options.SecretKey)
                .WithSSL(options.UseSsl)
                .Build();
        });

        services.AddSingleton<IObjectStorage, MinioObjectStorage>();
        services.AddHostedService<MinioBucketInitializer>();

        return services;
    }
}
