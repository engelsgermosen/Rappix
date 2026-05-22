using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Rappix.BuildingBlocks.Core.Storage;

namespace Rappix.BuildingBlocks.Storage;

/// <summary>
/// Almacenamiento en MinIO. Sube y borra con el endpoint interno (inyectado), pero firma las URLs de
/// lectura con un cliente aparte construido sobre el endpoint publico, para que la URL sea alcanzable
/// desde el navegador del host aunque las subidas usen el host interno (minio:9000 en Docker).
/// </summary>
internal sealed class MinioObjectStorage(IMinioClient uploadClient, IOptions<MinioOptions> options)
    : IObjectStorage, IDisposable
{
    private readonly string _bucket = options.Value.Bucket;

    private readonly IMinioClient _presignClient = new MinioClient()
        .WithEndpoint(options.Value.PublicEndpoint)
        .WithCredentials(options.Value.AccessKey, options.Value.SecretKey)
        .WithSSL(options.Value.UseSsl)
        .Build();

    public async Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        // MinIO exige el tamano del objeto; se bufferiza para garantizar un stream con longitud conocida.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        await uploadClient.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(_bucket)
                .WithObject(key)
                .WithStreamData(buffer)
                .WithObjectSize(buffer.Length)
                .WithContentType(contentType),
            cancellationToken);

        return key;
    }

    public Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _presignClient.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(_bucket)
                .WithObject(key)
                .WithExpiry((int)expiry.TotalSeconds));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        uploadClient.RemoveObjectAsync(
            new RemoveObjectArgs()
                .WithBucket(_bucket)
                .WithObject(key),
            cancellationToken);

    public void Dispose()
    {
        _presignClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
