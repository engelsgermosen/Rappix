using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Merchants.Application.Abstractions;

/// <summary>
/// Valida una imagen de logo: formato (magic bytes), dimensiones y relacion de aspecto.
/// El tamano maximo se verifica antes (en el endpoint). Devuelve el content-type detectado.
/// </summary>
public interface IImageValidator
{
    /// <summary>Valida el stream y devuelve el content-type MIME detectado, o un Result de fallo.</summary>
    Task<Result<string>> ValidateAsync(Stream imageStream, CancellationToken cancellationToken);
}
