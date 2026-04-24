namespace Appointment.Application.AppointmentsAggregates.CompleteAppointment;

public class CompleteAppointmentMessage
{
    public Guid AppointmentId { get; set; }
    public string? EventType { get; set; }
    public Guid EventId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? InsuredId { get; set; }
    public int ScheduleId { get; set; }
    public string? CountryISO { get; set; }
    public string? Status { get; set; }
    public string? ProcessedBy { get; set; }
}
