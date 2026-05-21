namespace Rappix.BuildingBlocks.Core.Results;

/// <summary>
/// Resultado de una operación que puede tener éxito o fallar con un Error tipado.
/// Evita el uso de excepciones para flujo de control.
/// </summary>
public readonly record struct Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    private Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Un resultado exitoso no puede tener error.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Un resultado fallido debe tener un error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.Success(value);
    public static Result<TValue> Failure<TValue>(Error error) => Result<TValue>.Failure(error);
}

public readonly record struct Result<TValue>
{
    private readonly TValue? _value;

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No se puede acceder al valor de un resultado fallido.");

    private Result(TValue value)
    {
        IsSuccess = true;
        _value = value;
        Error = Error.None;
    }

    private Result(Error error)
    {
        IsSuccess = false;
        _value = default;
        Error = error;
    }

    public static Result<TValue> Success(TValue value) => new(value);
    public static Result<TValue> Failure(Error error) => new(error);

    public static implicit operator Result<TValue>(TValue value) => Success(value);
    public static implicit operator Result<TValue>(Error error) => Failure(error);

    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<Error, TResult> onFailure)
        => IsSuccess ? onSuccess(_value!) : onFailure(Error);
}
