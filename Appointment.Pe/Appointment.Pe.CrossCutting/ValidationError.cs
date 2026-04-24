namespace Appointment.Pe.CrossCutting;

public sealed record ValidationError : Error
{
    public Error[] Errors { get; }

    public ValidationError(Error[] errors)
        : base("Validation.General", "One or more validation errors occurred", ErrorType.Validation)
    {
        Errors = errors;
    }

    public static ValidationError FromResults(IEnumerable<Result> results)
        => new([.. results.Where(x => x.IsFailure).Select(x => x.Error)]);
}
