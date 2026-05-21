namespace Rappix.Merchants.Infrastructure.Storage;

/// <summary>Configuracion de MinIO/S3 para el almacenamiento de logos.</summary>
public sealed class MinioOptions
{
    /// <summary>Nombre de la seccion de configuracion.</summary>
    public const string SectionName = "Minio";

    /// <summary>Endpoint interno para subir/borrar objetos (p.ej. minio:9000 dentro de Docker).</summary>
    public string Endpoint { get; set; } = "localhost:9000";

    /// <summary>
    /// Endpoint publico con el que se firman las URLs de lectura (p.ej. localhost:9000). Permite que
    /// la URL presignada sea alcanzable desde el navegador del host aunque las subidas usen el host interno.
    /// </summary>
    public string PublicEndpoint { get; set; } = "localhost:9000";

    /// <summary>Access key.</summary>
    public string AccessKey { get; set; } = "minioadmin";

    /// <summary>Secret key.</summary>
    public string SecretKey { get; set; } = "minioadmin";

    /// <summary>Bucket de los logos.</summary>
    public string Bucket { get; set; } = "merchants-logos";

    /// <summary>Usar TLS para conectarse a MinIO.</summary>
    public bool UseSsl { get; set; }
}
