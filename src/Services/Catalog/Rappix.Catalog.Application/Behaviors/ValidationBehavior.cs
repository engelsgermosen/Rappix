using System.Reflection;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Catalog.Application.Behaviors;

/// <summary>
/// Comportamiento de pipeline de MediatR que ejecuta los validadores de FluentValidation y,
/// ante errores, devuelve un Result (o Result de TValue) de fallo en lugar de lanzar una excepcion.
/// </summary>
/// <typeparam name="TRequest">Tipo de la peticion.</typeparam>
/// <typeparam name="TResponse">Tipo de la respuesta (Result o Result de TValue).</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);

        ValidationFailure[] failures =
        [
            .. (await Task.WhenAll(validators.Select(validator => validator.ValidateAsync(context, cancellationToken))))
                .SelectMany(result => result.Errors)
        ];

        if (failures.Length == 0)
        {
            return await next();
        }

        var error = Error.Validation(
            "Validation.Failed",
            string.Join(" ", failures.Select(failure => failure.ErrorMessage)));

        return CreateFailureResult(error);
    }

    private static TResponse CreateFailureResult(Error error)
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        Type valueType = typeof(TResponse).GetGenericArguments()[0];
        MethodInfo failureMethod = typeof(Result)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == nameof(Result.Failure) && method.IsGenericMethodDefinition)
            .MakeGenericMethod(valueType);

        return (TResponse)failureMethod.Invoke(null, [error])!;
    }
}
