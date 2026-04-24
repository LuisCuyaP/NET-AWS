namespace Appointment.Domain.Models;

public class ServiceResult<T>
{
    public T? Value { get; set; }
    public BackendErrorResponse? Error { get; set; }
    public bool Success => Error == null;
}

public class ServiceResult 
{ 
    public BackendErrorResponse? Error { get; set; }
    public bool Success => Error == null;
}
