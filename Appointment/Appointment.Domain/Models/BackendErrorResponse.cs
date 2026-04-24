namespace Appointment.Domain.Models;

public class BackendErrorResponse
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Status { get; set; }
    public string Detail { get; set; } = string.Empty;
    public List<BackendValidationItem> Errors { get; set; } = new();
}
