using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Rappix.BuildingBlocks.WebApi.Errors;

/// <summary>
/// Handler global de excepciones no manejadas.
/// Registrar con AddExceptionHandler, AddProblemDetails y UseExceptionHandler.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Excepcion no manejada: {Message}", exception.Message);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Ocurrio un error inesperado en el servidor.",
            Type = "https://rappix.dev/errors/unhandled-exception",
            Detail = "El servicio encontro un error procesando la solicitud."
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}