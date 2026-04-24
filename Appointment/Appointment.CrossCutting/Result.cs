using System.Diagnostics.CodeAnalysis;

namespace Appointment.CrossCutting;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }
    public Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None || !isSuccess && error == Error.None)
            throw new ArgumentException("Invalid Error", nameof(error));
        
        IsSuccess = isSuccess;
        Error = error;
    }
    public static Result Success() => new(true, Error.None);
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

public class Result<TValue>(TValue? value, bool isSuccess, Error error) : Result(isSuccess, error)
{
    private readonly TValue? _value = value;

    [NotNull]
    public TValue Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot access the value of a failed result.");
    public static implicit operator Result<TValue>(TValue value) => value is not null ? Success(value) : Failure<TValue>(Error.NullValue);
    public static Result<TValue> ValidationFailure(Error error) => new(default!, false, error);
}
