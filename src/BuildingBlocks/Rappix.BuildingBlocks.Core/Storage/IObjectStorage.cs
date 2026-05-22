namespace Rappix.BuildingBlocks.Core.Storage;

/// <summary>Almacenamiento de objetos (imagenes, comprobantes) en MinIO/S3.</summary>
public interface IObjectStorage
{
    /// <summary>Sube un objeto y devuelve su key.</summary>
    Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Genera una URL firmada de lectura con expiracion.</summary>
    Task<string> GetPresignedUrlAsync(string key, TimeSpan expiry, CancellationToken cancellationToken);

    /// <summary>Elimina un objeto.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
