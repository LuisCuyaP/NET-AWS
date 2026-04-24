namespace Appointment.Domain.Models;

public class BackendValidationItem
{
    public string? Code { get; set; }
    public string? Description { get; set; }
    public int? Type { get; set; } 
    public string? Field { get; set; }
}
