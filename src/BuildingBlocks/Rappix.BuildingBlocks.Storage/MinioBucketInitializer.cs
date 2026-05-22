using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace Rappix.BuildingBlocks.Storage;

/// <summary>
/// Crea el bucket configurado en MinIO al arrancar si aun no existe. No bloquea el arranque del
/// servicio si MinIO no responde todavia (se reintentara de forma natural en la primera subida).
/// </summary>
internal sealed partial class MinioBucketInitializer(
    IMinioClient client,
    IOptions<MinioOptions> options,
    ILogger<MinioBucketInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        string bucket = options.Value.Bucket;
        try
        {
            bool exists = await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), cancellationToken);
            if (exists)
            {
                LogBucketReady(logger, bucket);
                return;
            }

            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), cancellationToken);
            LogBucketCreated(logger, bucket);
        }
        catch (Exception exception) when (exception is MinioException or HttpRequestException)
        {
            LogBucketInitFailed(logger, bucket, exception);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Bucket '{Bucket}' ya existe en MinIO.")]
    private static partial void LogBucketReady(ILogger logger, string bucket);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bucket '{Bucket}' creado en MinIO.")]
    private static partial void LogBucketCreated(ILogger logger, string bucket);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No se pudo inicializar el bucket '{Bucket}' en MinIO; se intentara en la primera subida.")]
    private static partial void LogBucketInitFailed(ILogger logger, string bucket, Exception exception);
}
