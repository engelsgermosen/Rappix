using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.BuildingBlocks.WebApi.Endpoints;

public static class ResultExtensions
{
    /// <summary>
    /// Convierte un Result en IResult HTTP. En caso de exito retorna 200 con el valor.
    /// En caso de error, retorna el codigo HTTP apropiado con un ProblemDetails.
    /// </summary>
    public static IResult ToHttpResult<TValue>(this Result<TValue> result)
        => result.IsSuccess
            ? Results.Ok(result.Value)
            : ToProblem(result.Error);

    public static IResult ToHttpResult(this Result result)
        => result.IsSuccess
            ? Results.NoContent()
            : ToProblem(result.Error);

    /// <summary>
    /// Variante para POST: retorna 201 Created con location y body.
    /// </summary>
    public static IResult ToCreatedResult<TValue>(this Result<TValue> result, string location)
        => result.IsSuccess
            ? Results.Created(location, result.Value)
            : ToProblem(result.Error);

    private static IResult ToProblem(Error error)
    {
        var (status, title) = error.Type switch
        {
            ErrorType.Validation => (StatusCodes.Status400BadRequest, "Datos de entrada invalidos."),
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Recurso no encontrado."),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflicto con el estado actual."),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "No autenticado."),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "No autorizado."),
            _ => (StatusCodes.Status500InternalServerError, "Ocurrio un error inesperado.")
        };

        return Results.Problem(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = error.Description,
            Type = $"https://rappix.dev/errors/{error.Code}",
            Extensions = { ["code"] = error.Code }
        });
    }
}