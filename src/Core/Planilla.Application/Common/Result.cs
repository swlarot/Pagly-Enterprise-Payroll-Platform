namespace Vorluno.Planilla.Application.Common;

/// <summary>
/// Representa el resultado de una operación de servicio con éxito/fallo y mensaje opcional
/// </summary>
public class Result
{
    public bool Success { get; protected set; }
    public string? ErrorMessage { get; protected set; }

    protected Result(bool success, string? errorMessage = null)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }

    public static Result Ok() => new(true);
    public static Result Fail(string errorMessage) => new(false, errorMessage);
}

/// <summary>
/// Representa el resultado de una operación de servicio con un valor de retorno
/// </summary>
public class Result<T> : Result
{
    public T? Value { get; private set; }

    private Result(bool success, T? value, string? errorMessage = null)
        : base(success, errorMessage)
    {
        Value = value;
    }

    public static Result<T> Ok(T value) => new(true, value);
    public static new Result<T> Fail(string errorMessage) => new(false, default, errorMessage);
}
